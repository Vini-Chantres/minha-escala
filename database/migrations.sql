CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918023154_InitialCreate') THEN
    CREATE TABLE tenants (
        "Id" uuid NOT NULL,
        "Name" character varying(100) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_tenants" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918023154_InitialCreate') THEN
    CREATE TABLE audit_logs (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "UserId" uuid,
        "Action" character varying(50) NOT NULL,
        "Resource" character varying(100) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_audit_logs" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_audit_logs_tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES tenants ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918023154_InitialCreate') THEN
    CREATE TABLE invitations (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "Email" character varying(254) NOT NULL,
        "Hash" character varying(64) NOT NULL,
        "ExpiresAt" timestamp with time zone NOT NULL,
        "IsUsed" boolean NOT NULL,
        CONSTRAINT "PK_invitations" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_invitations_tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES tenants ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918023154_InitialCreate') THEN
    CREATE TABLE users (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "Name" character varying(100) NOT NULL,
        "Email" character varying(254) NOT NULL,
        "PasswordHash" character varying(256) NOT NULL,
        "Role" character varying(20) NOT NULL,
        "IsActive" boolean NOT NULL,
        "SecurityVersion" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_users" PRIMARY KEY ("Id"),
        CONSTRAINT "AK_users_Id_TenantId" UNIQUE ("Id", "TenantId"),
        CONSTRAINT "CK_users_role" CHECK ("Role" IN ('Owner', 'Member')),
        CONSTRAINT "FK_users_tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES tenants ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918023154_InitialCreate') THEN
    CREATE TABLE auth_sessions (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "IsRevoked" boolean NOT NULL,
        "ExpiresAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_auth_sessions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_auth_sessions_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918023154_InitialCreate') THEN
    CREATE TABLE password_resets (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "Hash" character varying(64) NOT NULL,
        "ExpiresAt" timestamp with time zone NOT NULL,
        "IsUsed" boolean NOT NULL,
        CONSTRAINT "PK_password_resets" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_password_resets_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918023154_InitialCreate') THEN
    CREATE TABLE schedule_entries (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "Date" date NOT NULL,
        "Type" character varying(20) NOT NULL,
        "Sector" character varying(120) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_schedule_entries" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_entries_type" CHECK ("Type" IN ('folga', 'trabalho')),
        CONSTRAINT "FK_schedule_entries_users_UserId_TenantId" FOREIGN KEY ("UserId", "TenantId") REFERENCES users ("Id", "TenantId") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918023154_InitialCreate') THEN
    CREATE TABLE refresh_tokens (
        "Id" uuid NOT NULL,
        "SessionId" uuid NOT NULL,
        "Hash" character varying(64) NOT NULL,
        "ConsumedAt" timestamp with time zone,
        CONSTRAINT "PK_refresh_tokens" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_refresh_tokens_auth_sessions_SessionId" FOREIGN KEY ("SessionId") REFERENCES auth_sessions ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918023154_InitialCreate') THEN
    CREATE INDEX "IX_audit_logs_TenantId_CreatedAt" ON audit_logs ("TenantId", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918023154_InitialCreate') THEN
    CREATE INDEX "IX_auth_sessions_UserId" ON auth_sessions ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918023154_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_invitations_Hash" ON invitations ("Hash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918023154_InitialCreate') THEN
    CREATE INDEX "IX_invitations_TenantId" ON invitations ("TenantId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918023154_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_password_resets_Hash" ON password_resets ("Hash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918023154_InitialCreate') THEN
    CREATE INDEX "IX_password_resets_UserId" ON password_resets ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918023154_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_refresh_tokens_Hash" ON refresh_tokens ("Hash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918023154_InitialCreate') THEN
    CREATE INDEX "IX_refresh_tokens_SessionId" ON refresh_tokens ("SessionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918023154_InitialCreate') THEN
    CREATE INDEX "IX_schedule_entries_TenantId_UserId" ON schedule_entries ("TenantId", "UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918023154_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_schedule_entries_UserId_Date" ON schedule_entries ("UserId", "Date");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918023154_InitialCreate') THEN
    CREATE INDEX "IX_schedule_entries_UserId_TenantId" ON schedule_entries ("UserId", "TenantId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918023154_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_users_Email" ON users ("Email");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918023154_InitialCreate') THEN
    CREATE INDEX "IX_users_TenantId" ON users ("TenantId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918023154_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260918023154_InitialCreate', '10.0.12');
    END IF;
END $EF$;
COMMIT;

