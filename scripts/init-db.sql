-- GrapheneTrace Database Initialization Script
-- ASP.NET Core Identity Schema for PostgreSQL
-- Author: SID:2412494

-- Enable required extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_stat_statements";

-- ============================================================
-- ASP.NET CORE IDENTITY TABLES
-- ============================================================

-- AspNetUsers - Main user table with custom fields
CREATE TABLE IF NOT EXISTS "AspNetUsers" (
    "Id" uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    "UserName" varchar(256),
    "NormalizedUserName" varchar(256),
    "Email" varchar(256),
    "NormalizedEmail" varchar(256),
    "EmailConfirmed" boolean NOT NULL DEFAULT false,
    "PasswordHash" text,
    "SecurityStamp" text,
    "ConcurrencyStamp" text,
    "PhoneNumber" text,
    "PhoneNumberConfirmed" boolean NOT NULL DEFAULT false,
    "TwoFactorEnabled" boolean NOT NULL DEFAULT false,
    "LockoutEnd" timestamptz,
    "LockoutEnabled" boolean NOT NULL DEFAULT true,
    "AccessFailedCount" integer NOT NULL DEFAULT 0,

    -- Custom fields for GrapheneTrace
    "FirstName" varchar(100) NOT NULL,
    "LastName" varchar(100) NOT NULL,
    "UserType" varchar(20) NOT NULL CHECK ("UserType" IN ('admin', 'clinician', 'patient')),
    "DeactivatedAt" timestamptz,
    "CreatedAt" timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- AspNetRoles - Role definitions
CREATE TABLE IF NOT EXISTS "AspNetRoles" (
    "Id" uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    "Name" varchar(256),
    "NormalizedName" varchar(256),
    "ConcurrencyStamp" text
);

-- AspNetUserRoles - User-Role mapping (many-to-many)
CREATE TABLE IF NOT EXISTS "AspNetUserRoles" (
    "UserId" uuid NOT NULL,
    "RoleId" uuid NOT NULL,
    PRIMARY KEY ("UserId", "RoleId"),
    CONSTRAINT "FK_AspNetUserRoles_AspNetUsers_UserId"
        FOREIGN KEY ("UserId") REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_AspNetUserRoles_AspNetRoles_RoleId"
        FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles"("Id") ON DELETE CASCADE
);

-- AspNetUserClaims - Custom user claims
CREATE TABLE IF NOT EXISTS "AspNetUserClaims" (
    "Id" serial PRIMARY KEY,
    "UserId" uuid NOT NULL,
    "ClaimType" text,
    "ClaimValue" text,
    CONSTRAINT "FK_AspNetUserClaims_AspNetUsers_UserId"
        FOREIGN KEY ("UserId") REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE
);

-- AspNetUserLogins - External login providers (Google, Microsoft, etc.)
CREATE TABLE IF NOT EXISTS "AspNetUserLogins" (
    "LoginProvider" varchar(128) NOT NULL,
    "ProviderKey" varchar(128) NOT NULL,
    "ProviderDisplayName" text,
    "UserId" uuid NOT NULL,
    PRIMARY KEY ("LoginProvider", "ProviderKey"),
    CONSTRAINT "FK_AspNetUserLogins_AspNetUsers_UserId"
        FOREIGN KEY ("UserId") REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE
);

-- AspNetUserTokens - Authentication tokens (password reset, email confirmation, etc.)
CREATE TABLE IF NOT EXISTS "AspNetUserTokens" (
    "UserId" uuid NOT NULL,
    "LoginProvider" varchar(128) NOT NULL,
    "Name" varchar(128) NOT NULL,
    "Value" text,
    PRIMARY KEY ("UserId", "LoginProvider", "Name"),
    CONSTRAINT "FK_AspNetUserTokens_AspNetUsers_UserId"
        FOREIGN KEY ("UserId") REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE
);

-- AspNetRoleClaims - Claims per role
CREATE TABLE IF NOT EXISTS "AspNetRoleClaims" (
    "Id" serial PRIMARY KEY,
    "RoleId" uuid NOT NULL,
    "ClaimType" text,
    "ClaimValue" text,
    CONSTRAINT "FK_AspNetRoleClaims_AspNetRoles_RoleId"
        FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles"("Id") ON DELETE CASCADE
);

-- ============================================================
-- INDEXES FOR IDENTITY TABLES (Performance optimization)
-- ============================================================

-- AspNetUsers indexes
CREATE UNIQUE INDEX IF NOT EXISTS "UserNameIndex" ON "AspNetUsers" ("NormalizedUserName") WHERE "NormalizedUserName" IS NOT NULL;
CREATE INDEX IF NOT EXISTS "EmailIndex" ON "AspNetUsers" ("NormalizedEmail");
CREATE INDEX IF NOT EXISTS "IX_AspNetUsers_UserType" ON "AspNetUsers" ("UserType") WHERE "DeactivatedAt" IS NULL;
CREATE INDEX IF NOT EXISTS "IX_AspNetUsers_DeactivatedAt" ON "AspNetUsers" ("DeactivatedAt") WHERE "DeactivatedAt" IS NOT NULL;

-- AspNetRoles indexes
CREATE UNIQUE INDEX IF NOT EXISTS "RoleNameIndex" ON "AspNetRoles" ("NormalizedName") WHERE "NormalizedName" IS NOT NULL;

-- AspNetUserRoles indexes
CREATE INDEX IF NOT EXISTS "IX_AspNetUserRoles_RoleId" ON "AspNetUserRoles" ("RoleId");

-- AspNetUserClaims indexes
CREATE INDEX IF NOT EXISTS "IX_AspNetUserClaims_UserId" ON "AspNetUserClaims" ("UserId");

-- AspNetUserLogins indexes
CREATE INDEX IF NOT EXISTS "IX_AspNetUserLogins_UserId" ON "AspNetUserLogins" ("UserId");

-- AspNetRoleClaims indexes
CREATE INDEX IF NOT EXISTS "IX_AspNetRoleClaims_RoleId" ON "AspNetRoleClaims" ("RoleId");

-- ============================================================
-- FUNCTIONS & TRIGGERS
-- ============================================================

-- Trigger function to update UpdatedAt timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW."UpdatedAt" = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Apply UpdatedAt trigger to AspNetUsers table
DROP TRIGGER IF EXISTS update_aspnetusers_updated_at ON "AspNetUsers";
CREATE TRIGGER update_aspnetusers_updated_at
    BEFORE UPDATE ON "AspNetUsers"
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- ============================================================
-- MONITORING & METRICS VIEWS
-- ============================================================

-- User metrics view (compatible with Identity schema)
CREATE OR REPLACE VIEW user_metrics AS
SELECT
    "UserType",
    COUNT(*) as user_count,
    COUNT(*) FILTER (WHERE "DeactivatedAt" IS NULL) as active_user_count,
    COUNT(*) FILTER (WHERE "LockoutEnd" > NOW()) as locked_out_count,
    COUNT(*) FILTER (WHERE "EmailConfirmed" = true) as email_confirmed_count,
    COUNT(*) FILTER (WHERE "TwoFactorEnabled" = true) as two_factor_enabled_count
FROM "AspNetUsers"
GROUP BY "UserType";

-- Database health check view
CREATE OR REPLACE VIEW db_health AS
SELECT
    NOW() as check_time,
    pg_database_size(current_database()) as database_size_bytes,
    (SELECT COUNT(*) FROM "AspNetUsers") as total_users,
    (SELECT COUNT(*) FROM "AspNetUsers" WHERE "DeactivatedAt" IS NULL) as active_users,
    (SELECT COUNT(*) FROM "AspNetUsers" WHERE "LockoutEnd" > NOW()) as locked_users,
    (SELECT version()) as postgres_version;

-- Authentication audit view (for HIPAA compliance tracking)
CREATE OR REPLACE VIEW auth_audit_summary AS
SELECT
    "UserType",
    COUNT(*) FILTER (WHERE "AccessFailedCount" > 0) as users_with_failed_attempts,
    AVG("AccessFailedCount") as avg_failed_attempts,
    COUNT(*) FILTER (WHERE "LockoutEnd" > NOW()) as currently_locked_out
FROM "AspNetUsers"
WHERE "DeactivatedAt" IS NULL
GROUP BY "UserType";

-- ============================================================
-- GRANTS FOR MONITORING
-- ============================================================

-- Grant SELECT on monitoring views to public for Prometheus/Grafana
GRANT SELECT ON user_metrics TO PUBLIC;
GRANT SELECT ON db_health TO PUBLIC;
GRANT SELECT ON auth_audit_summary TO PUBLIC;
GRANT SELECT ON pg_stat_statements TO PUBLIC;

-- ============================================================
-- SEED DATA (Optional - for testing)
-- ============================================================

-- Note: Passwords should be hashed by Identity in the application
-- This is just a placeholder comment for future seed data if needed
-- Example: INSERT INTO "AspNetUsers" (...) VALUES (...);

-- ============================================================
-- COMPLETION NOTICE
-- ============================================================

DO $$
BEGIN
    RAISE NOTICE '==============================================';
    RAISE NOTICE 'GrapheneTrace Database Initialized';
    RAISE NOTICE 'ASP.NET Core Identity Schema Created';
    RAISE NOTICE '==============================================';
    RAISE NOTICE 'Tables created:';
    RAISE NOTICE '  - AspNetUsers (with custom fields)';
    RAISE NOTICE '  - AspNetRoles';
    RAISE NOTICE '  - AspNetUserRoles';
    RAISE NOTICE '  - AspNetUserClaims';
    RAISE NOTICE '  - AspNetUserLogins';
    RAISE NOTICE '  - AspNetUserTokens';
    RAISE NOTICE '  - AspNetRoleClaims';
    RAISE NOTICE '==============================================';
    RAISE NOTICE 'Monitoring views available:';
    RAISE NOTICE '  - user_metrics';
    RAISE NOTICE '  - db_health';
    RAISE NOTICE '  - auth_audit_summary';
    RAISE NOTICE '==============================================';
    RAISE NOTICE 'Custom AspNetUsers fields:';
    RAISE NOTICE '  - FirstName, LastName';
    RAISE NOTICE '  - UserType (admin/clinician/patient)';
    RAISE NOTICE '  - DeactivatedAt (soft deletion)';
    RAISE NOTICE '  - CreatedAt, UpdatedAt';
    RAISE NOTICE '==============================================';
END $$;
