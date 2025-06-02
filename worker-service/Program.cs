using System;
using System.Data.Common;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Newtonsoft.Json;
using Npgsql;
using StackExchange.Redis;

namespace Worker
{
    public class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                var connStr = Environment.GetEnvironmentVariable("POSTGRES_CONN");
                var redisHost = Environment.GetEnvironmentVariable("REDIS_HOST");

                Console.WriteLine("POSTGRES_CONN: " + connStr);
                Console.WriteLine("REDIS_HOST: " + redisHost);

                if (string.IsNullOrWhiteSpace(connStr))
                {
                    Console.Error.WriteLine("POSTGRES_CONN environment variable is not set.");
                    return 1;
                }

                if (string.IsNullOrWhiteSpace(redisHost))
                {
                    Console.Error.WriteLine("REDIS_HOST environment variable is not set.");
                    return 1;
                }

                var pgsql = OpenDbConnection(connStr);
                var redisConn = OpenRedisConnection(redisHost);
                var redis = redisConn.GetDatabase();

                var keepAliveCommand = pgsql.CreateCommand();
                keepAliveCommand.CommandText = "SELECT 1";

                var definition = new { vote = "", voter_id = "" };
                while (true)
                {
                    Thread.Sleep(100);

                    if (redisConn == null || !redisConn.IsConnected)
                    {
                        Console.WriteLine("Reconnecting Redis");
                        redisConn = OpenRedisConnection(redisHost);
                        redis = redisConn.GetDatabase();
                    }

                    string json = redis.ListLeftPopAsync("votes").Result;
                    if (json != null)
                    {
                        var vote = JsonConvert.DeserializeAnonymousType(json, definition);
                        Console.WriteLine($"Processing vote for '{vote.vote}' by '{vote.voter_id}'");

                        if (!pgsql.State.Equals(System.Data.ConnectionState.Open))
                        {
                            Console.WriteLine("Reconnecting DB");
                            pgsql = OpenDbConnection(connStr);
                        }
                        else
                        {
                            UpdateVote(pgsql, vote.voter_id, vote.vote);
                        }
                    }
                    else
                    {
                        keepAliveCommand.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Fatal error:");
                Console.Error.WriteLine(ex.ToString());
                return 1;
            }
        }

        private static NpgsqlConnection OpenDbConnection(string connectionString)
        {
            NpgsqlConnection connection;

            while (true)
            {
                try
                {
                    connection = new NpgsqlConnection(connectionString);
                    connection.Open();
                    break;
                }
                catch (SocketException e)
                {
                    Console.Error.WriteLine($"Waiting for db: {e.Message}");
                    Thread.Sleep(1000);
                }
                catch (DbException e)
                {
                    Console.Error.WriteLine($"Waiting for db: {e.Message}");
                    Thread.Sleep(1000);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"Unexpected error: {e.Message}");
                    Thread.Sleep(1000);
                }
            }

            Console.Error.WriteLine("Connected to db");

            var command = connection.CreateCommand();
            command.CommandText = @"CREATE TABLE IF NOT EXISTS votes (
                                        id VARCHAR(255) NOT NULL UNIQUE,
                                        vote VARCHAR(255) NOT NULL
                                    )";
            command.ExecuteNonQuery();

            return connection;
        }

        private static ConnectionMultiplexer OpenRedisConnection(string hostname)
        {
            string ipAddress;
            try
            {
                ipAddress = GetIp(hostname);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to resolve Redis host '{hostname}': {ex.Message}");
                throw;
            }

            Console.WriteLine($"Found Redis at {ipAddress}");

            while (true)
            {
                try
                {
                    Console.Error.WriteLine("Connecting to Redis");
                    return ConnectionMultiplexer.Connect(ipAddress);
                }
                catch (RedisConnectionException e)
                {
                    Console.Error.WriteLine($"Waiting for Redis: {e.Message}");
                    Thread.Sleep(1000);
                }
            }
        }

        private static string GetIp(string hostname)
            => Dns.GetHostEntryAsync(hostname)
                .Result
                .AddressList
                .First(a => a.AddressFamily == AddressFamily.InterNetwork)
                .ToString();

        private static void UpdateVote(NpgsqlConnection connection, string voterId, string vote)
        {
            var command = connection.CreateCommand();
            try
            {
                command.CommandText = "INSERT INTO votes (id, vote) VALUES (@id, @vote)";
                command.Parameters.AddWithValue("@id", voterId);
                command.Parameters.AddWithValue("@vote", vote);
                command.ExecuteNonQuery();
            }
            catch (DbException)
            {
                command.CommandText = "UPDATE votes SET vote = @vote WHERE id = @id";
                command.ExecuteNonQuery();
            }
            finally
            {
                command.Dispose();
            }
        }
    }
}
