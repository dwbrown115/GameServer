-- use MySecureApp;
-- GO
-- -- CREATE SCHEMA auth;
-- -- GO

-- CREATE SCHEMA auth;
-- GO

-- CREATE TABLE auth.RefreshTokenRecord (
--     Id INT IDENTITY(1,1) PRIMARY KEY,
--     UserId NVARCHAR(100) NOT NULL,
--     DeviceId NVARCHAR(100) NOT NULL,
--     EncryptedRefreshToken NVARCHAR(MAX) NOT NULL,
--     SecretKey VARBINARY(MAX) NOT NULL,
--     ExpiresAt DATETIME NOT NULL,
--     IsRevoked BIT NOT NULL DEFAULT 0
-- );


-- DROP TABLE auth.JwtTokens;


-- DELETE FROM users.Users           
-- WHERE Id BETWEEN 1 and 1000000;                
SELECT * FROM users.Users;
-- DROP TABLE users.Users;

-- ALTER TABLE users.Users
-- DROP COLUMN Id;

-- ALTER TABLE users.Users
-- ADD Id INT IDENTITY(1,1) PRIMARY KEY;