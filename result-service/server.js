const express = require('express');
const async = require('async');
const { Pool } = require('pg');
const cookieParser = require('cookie-parser');
const path = require('path');
const http = require('http');
const socketIO = require('socket.io');

const app = express();
const server = http.createServer(app);
const io = socketIO(server);

// Use port 80 or env-defined port
const port = process.env.PORT || 80;

// PostgreSQL connection
const pool = new Pool({
  connectionString: process.env.POSTGRES_CONN || 'postgres://postgres:postgres@10.0.139.153/postgres'
});

// Middleware
app.use(cookieParser());
app.use(express.urlencoded({ extended: true }));

// Serve static assets from 'views' (including index.html, scripts, CSS)
const viewsPath = path.join(__dirname, 'views');
app.use(express.static(viewsPath));

// Route: root (serve the UI)
app.get('/', (req, res) => {
  res.sendFile(path.join(viewsPath, 'index.html'), (err) => {
    if (err) {
      console.error('Error sending index.html:', err);
      res.status(500).send('Error loading index page.');
    }
  });
});

// WebSocket connection
io.on('connection', (socket) => {
  console.log('Client connected via socket');

  socket.emit('message', { text: 'Welcome!' });

  socket.on('subscribe', (data) => {
    socket.join(data.channel);
  });
});

// Retry DB connection until successful
async.retry(
  { times: 1000, interval: 1000 },
  (callback) => {
    pool.connect((err, client) => {
      if (err) {
        console.error('Waiting for DB', err.message);
      }
      callback(err, client);
    });
  },
  (err, client) => {
    if (err) {
      return console.error('Giving up connecting to DB');
    }
    console.log('Connected to DB');
    getVotes(client);
  }
);

// Poll database and emit vote counts
function getVotes(client) {
  client.query('SELECT vote, COUNT(id) AS count FROM votes GROUP BY vote', [], (err, result) => {
    if (err) {
      console.error('Error performing query:', err);
    } else {
      const votes = collectVotesFromResult(result);
      io.sockets.emit('scores', votes); // ✅ emit raw object, not string
    }
    setTimeout(() => getVotes(client), 2000); // Poll every 2 seconds
  });
}

// Aggregate votes from DB result
function collectVotesFromResult(result) {
  const votes = { a: 0, b: 0 };

  result.rows.forEach((row) => {
    const vote = row.vote;
    const count = parseInt(row.count);

    if (vote === 'a' || vote === 'b') {
      votes[vote] = count;
    }
  });

  return votes;
}

// Start HTTP server
server.listen(port, () => {
  console.log(`Result service running on port ${port}`);
});
