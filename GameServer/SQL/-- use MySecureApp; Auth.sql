-- use MySecureApp;
-- GO

DELETE FROM auth.RefreshTokenRecord    
WHERE Id BETWEEN 1 and 1000000;

SELECT * FROM auth.RefreshTokenRecord;

