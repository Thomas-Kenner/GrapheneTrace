-- GrapheneTrace Database Initialization Script
-- Minimal schema for authentication and monitoring setup

-- Enable required extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_stat_statements";

-- ============================================================
-- USER MANAGEMENT TABLES
-- ============================================================

-- Users table - stores all users (admin, clinician, patient)
CREATE TABLE IF NOT EXISTS users (
    user_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    username VARCHAR(50) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    user_type VARCHAR(20) NOT NULL CHECK (user_type IN ('admin', 'clinician', 'patient')),
    email VARCHAR(255) UNIQUE NOT NULL,
    first_name VARCHAR(100) NOT NULL,
    last_name VARCHAR(100) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    deactivated_at TIMESTAMP WITH TIME ZONE DEFAULT NULL
);

-- Index for login queries (email lookup, active accounts only)
CREATE INDEX idx_users_email_active ON users(email) WHERE deactivated_at IS NULL;

-- Index for role-based queries (active accounts only)
CREATE INDEX idx_users_user_type_active ON users(user_type) WHERE deactivated_at IS NULL;

-- ============================================================================
-- MONITORING & METRICS SETUP
-- ============================================================================

-- Create a view for Prometheus/Grafana to monitor user counts by role
CREATE OR REPLACE VIEW user_metrics AS
SELECT
    user_type,
    COUNT(*) as user_count,
    COUNT(*) FILTER (WHERE deactivated_at IS NULL) as active_user_count,
    COUNT(*) FILTER (WHERE last_login_at > NOW() - INTERVAL '7 days' AND deactivated_at IS NULL) as active_last_7_days,
    COUNT(*) FILTER (WHERE last_login_at > NOW() - INTERVAL '30 days' AND deactivated_at IS NULL) as active_last_30_days
FROM users
GROUP BY user_type;

-- Database health check view
CREATE OR REPLACE VIEW db_health AS
SELECT
    NOW() as check_time,
    pg_database_size(current_database()) as database_size_bytes,
    (SELECT COUNT(*) FROM users) as total_users,
    (SELECT COUNT(*) FROM users WHERE deactivated_at IS NULL) as active_users,
    (SELECT version()) as postgres_version;


-- ============================================================================
-- FUNCTIONS
-- ============================================================================

-- Trigger function to update updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Apply updated_at trigger to users table
CREATE TRIGGER update_users_updated_at
    BEFORE UPDATE ON users
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- ============================================================================
-- GRANTS FOR MONITORING
-- ============================================================================

-- Grant SELECT on monitoring views to public for Prometheus/Grafana
GRANT SELECT ON user_metrics TO PUBLIC;
GRANT SELECT ON db_health TO PUBLIC;
GRANT SELECT ON pg_stat_statements TO PUBLIC;

-- ============================================================================
-- COMPLETION NOTICE
-- ============================================================================

DO $$
BEGIN
    RAISE NOTICE '==============================================';
    RAISE NOTICE 'GrapheneTrace Database Initialized';
    RAISE NOTICE '==============================================';
    RAISE NOTICE 'Monitoring views available:';
    RAISE NOTICE '  - user_metrics (for Grafana dashboards)';
    RAISE NOTICE '  - db_health (database health check)';
    RAISE NOTICE '==============================================';
END $$;
