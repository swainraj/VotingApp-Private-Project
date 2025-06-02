# Define a base stage that uses the official python runtime base image
FROM python:3.11-slim AS base

# Add curl for healthcheck
RUN apt-get update && \
    apt-get install -y --no-install-recommends curl && \
    rm -rf /var/lib/apt/lists/*

# Set the application directory
WORKDIR /usr/local/app

# Install our requirements.txt
COPY requirements.txt ./requirements.txt
RUN pip install --no-cache-dir -r requirements.txt

# Define a stage specifically for development
FROM base AS dev
RUN pip install watchdog
ENV FLASK_ENV=development
CMD ["python", "app.py"]

# Define the final stage for production
FROM base AS final

# Copy application code
COPY . .

# Expose port 80
EXPOSE 80

# Run Gunicorn bound to IPv6-compatible address
CMD ["gunicorn", "app:app", "-b", "[::]:80", "--log-file", "-", "--access-logfile", "-", "--workers", "4", "--keep-alive", "0"]
