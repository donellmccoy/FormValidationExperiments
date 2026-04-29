SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRAN;
-- Repoint local Bookmarks from old admin user to incoming remote admin user (admin@ect.mil)
UPDATE Bookmarks SET UserId = 'aa6be79c-ee31-45e1-9aed-48c8a68e9264' WHERE UserId = '0b3fdf71-c10b-40e3-a97c-aa348d881464';
DELETE FROM AspNetUserRoles;
DELETE FROM AspNetUserClaims;
DELETE FROM AspNetUserLogins;
DELETE FROM AspNetUserTokens;
DELETE FROM AspNetRoleClaims;
DELETE FROM AspNetRoles;
DELETE FROM AspNetUsers;
INSERT INTO AspNetUsers ([Id],[UserName],[NormalizedUserName],[Email],[NormalizedEmail],[EmailConfirmed],[PasswordHash],[SecurityStamp],[ConcurrencyStamp],[PhoneNumber],[PhoneNumberConfirmed],[TwoFactorEnabled],[LockoutEnd],[LockoutEnabled],[AccessFailedCount],[FirstName],[LastName]) VALUES ('a8043ac0-c64c-4318-b621-5ca404db7085','test@test.com','TEST@TEST.COM','test@test.com','TEST@TEST.COM',0,'AQAAAAIAAYagAAAAECgvhCsZHoELQ90IGCwt94kIKEbohUQYL69USDVIPlMB+FrGHggdUiFgWswq8Z+Fqw==','TBY5QM6V3Y42MHEVMPMWFZS5Y7KL5WCV','910c9f74-6707-42ab-8195-66453132676e',NULL,0,0,NULL,1,4,NULL,NULL);
INSERT INTO AspNetUsers ([Id],[UserName],[NormalizedUserName],[Email],[NormalizedEmail],[EmailConfirmed],[PasswordHash],[SecurityStamp],[ConcurrencyStamp],[PhoneNumber],[PhoneNumberConfirmed],[TwoFactorEnabled],[LockoutEnd],[LockoutEnabled],[AccessFailedCount],[FirstName],[LastName]) VALUES ('aa6be79c-ee31-45e1-9aed-48c8a68e9264','admin@ect.mil','ADMIN@ECT.MIL','admin@ect.mil','ADMIN@ECT.MIL',1,'AQAAAAIAAYagAAAAEE54d1dmj8OYEXtY7ibhekZOIqa5PzK7fznJ+eOHXtlk2YwttDHWE41tJSzZl09yJA==','AIPY7BRJGS4XS6XUZ57JYWLPHVWW4Y3I','8316e514-6c92-42d4-9af7-934dc04309f0',NULL,0,0,NULL,1,0,NULL,NULL);
INSERT INTO AspNetUsers ([Id],[UserName],[NormalizedUserName],[Email],[NormalizedEmail],[EmailConfirmed],[PasswordHash],[SecurityStamp],[ConcurrencyStamp],[PhoneNumber],[PhoneNumberConfirmed],[TwoFactorEnabled],[LockoutEnd],[LockoutEnabled],[AccessFailedCount],[FirstName],[LastName]) VALUES ('c6659170-2072-45b8-a1d1-3b596fd2befa','testadmin@test.com','TESTADMIN@TEST.COM','testadmin@test.com','TESTADMIN@TEST.COM',0,'AQAAAAIAAYagAAAAEPbF8oHKJj7orC4n+Rk2K3D7pzIJi0hU8+Lc/1U8OJKAbGvrtwAV72DHzcF1JgpYvQ==','RF6AJSIM7IPQB5QYBNWWIPD6ZA4XRNBH','2cd333b0-b997-467b-9266-4d5c8beabcc9',NULL,0,0,NULL,1,0,NULL,NULL);
COMMIT;
SELECT COUNT(*) AS LocalUsersAfter FROM AspNetUsers;

