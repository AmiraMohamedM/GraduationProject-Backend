-- ============================================================================
-- Database Initialization Script
-- ============================================================================
-- This script ensures the database exists and is ready
-- Place this file as: init-db.sql in your project root
-- ============================================================================

-- Create database if it doesn't exist (PostgreSQL syntax)
SELECT 'CREATE DATABASE graduation_db'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'graduation_db')\gexec

-- Connect to the database
\c graduation_db

-- Optional: Create extension for UUID support (if you use GUIDs)
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Log success
SELECT 'Database graduation_db is ready!' as status;