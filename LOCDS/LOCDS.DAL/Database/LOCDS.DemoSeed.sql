SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/*
  Demo seed dataset for LOCDS.
  - Uses deterministic GUIDs to keep cross-table references stable.
  - Safe to re-run: clears dependent tables before inserting.
*/

DELETE FROM locds.AuditLog;
DELETE FROM locds.LoanOffer;
DELETE FROM locds.UnderwritingDecision;
DELETE FROM locds.CreditBureauReport;
DELETE FROM locds.LoanApplication;
DELETE FROM locds.Applicant;
GO

DECLARE @Now DATETIME2(0) = SYSUTCDATETIME();

DECLARE @Applicant1 UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111';
DECLARE @Applicant2 UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222222';
DECLARE @Applicant3 UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333333';
DECLARE @Applicant4 UNIQUEIDENTIFIER = '44444444-4444-4444-4444-444444444444';
DECLARE @Applicant5 UNIQUEIDENTIFIER = '55555555-5555-5555-5555-555555555555';

DECLARE @Application1 UNIQUEIDENTIFIER = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1';
DECLARE @Application2 UNIQUEIDENTIFIER = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2';
DECLARE @Application3 UNIQUEIDENTIFIER = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3';
DECLARE @Application4 UNIQUEIDENTIFIER = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa4';
DECLARE @Application5 UNIQUEIDENTIFIER = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa5';

INSERT INTO locds.Applicant
(
    ApplicantId,
    FirstName,
    LastName,
    DOB,
    PAN,
    AadhaarHash,
    AnnualIncome,
    EmploymentType,
    CreditScore,
    RiskTier,
    CreatedDate,
    LastModifiedDate
)
VALUES
(@Applicant1, N'Priya', N'Sharma', '1990-03-11', N'ABCDE1234F', N'HASH-PRIYA', 1800000, 0, 748, 1, DATEADD(DAY, -30, @Now), @Now),
(@Applicant2, N'Rahul', N'Gupta', '1988-09-14', N'PQRSX5678Z', N'HASH-RAHUL', 1200000, 1, 701, 1, DATEADD(DAY, -20, @Now), @Now),
(@Applicant3, N'Anita', N'Desai', '1992-02-01', N'LMNOP3456Q', N'HASH-ANITA', 2200000, 2, 689, 2, DATEADD(DAY, -18, @Now), @Now),
(@Applicant4, N'Vikram', N'Singh', '1985-12-23', N'UVWXY9876T', N'HASH-VIKRAM', 950000, 3, 662, 2, DATEADD(DAY, -10, @Now), @Now),
(@Applicant5, N'Neha', N'Patel', '1994-07-05', N'QWERT1234Y', N'HASH-NEHA', 1500000, 0, 733, 1, DATEADD(DAY, -5, @Now), @Now);

INSERT INTO locds.LoanApplication
(
    ApplicationId,
    ApplicantId,
    LoanAmount,
    Tenure,
    Purpose,
    Status,
    CreatedDate,
    LastModifiedDate,
    AssignedUnderwriterId
)
VALUES
(@Application1, @Applicant1, 750000, 180, N'Home', 1, DATEADD(DAY, -7, @Now), @Now, NULL),
(@Application2, @Applicant2, 500000, 60, N'Personal', 2, DATEADD(DAY, -3, @Now), @Now, NULL),
(@Application3, @Applicant3, 1200000, 240, N'Home', 3, DATEADD(DAY, -6, @Now), @Now, '99999999-9999-9999-9999-999999999999'),
(@Application4, @Applicant4, 300000, 48, N'Vehicle', 4, DATEADD(DAY, -2, @Now), @Now, '99999999-9999-9999-9999-999999999999'),
(@Application5, @Applicant5, 600000, 120, N'Education', 2, DATEADD(DAY, -1, @Now), @Now, NULL);

INSERT INTO locds.CreditBureauReport
(
    ApplicationId,
    Bureau,
    Score,
    Enquiries,
    ActiveLoans,
    DefaultHistory,
    PulledAt,
    CreatedDate,
    LastModifiedDate
)
VALUES
(@Application2, 0, 712, 1, 1, 0, DATEADD(HOUR, -8, @Now), @Now, @Now),
(@Application2, 1, 705, 1, 1, 0, DATEADD(HOUR, -8, @Now), @Now, @Now),
(@Application3, 0, 684, 3, 2, 0, DATEADD(DAY, -1, @Now), @Now, @Now),
(@Application3, 2, 691, 2, 2, 0, DATEADD(DAY, -1, @Now), @Now, @Now),
(@Application5, 0, 729, 0, 1, 0, DATEADD(HOUR, -3, @Now), @Now, @Now);

INSERT INTO locds.UnderwritingDecision
(
    ApplicationId,
    RecommendedAction,
    ApprovedAmount,
    InterestRate,
    Tenure,
    RiskScore,
    Remarks,
    DecidedBy,
    DecidedAt,
    CreatedDate,
    LastModifiedDate
)
VALUES
(@Application3, 0, 1000000, 10.7500, 240, 0.7200, N'Stable income and acceptable leverage.', N'under.writer', DATEADD(HOUR, -4, @Now), @Now, @Now),
(@Application4, 0, 300000, 11.2500, 48, 0.6900, N'Approved under standard policy.', N'under.writer', DATEADD(DAY, -1, @Now), @Now, @Now);

INSERT INTO locds.LoanOffer
(
    ApplicationId,
    EMI,
    ProcessingFee,
    TotalCost,
    ValidUntil,
    IsAccepted,
    CreatedDate,
    LastModifiedDate
)
VALUES
(@Application4, 7800, 1500, 374400, DATEADD(DAY, 15, @Now), 0, @Now, @Now);

INSERT INTO locds.AuditLog
(
    EntityType,
    EntityId,
    Action,
    OldValue,
    NewValue,
    PerformedBy,
    PerformedAt,
    IPAddress,
    CreatedDate,
    LastModifiedDate
)
VALUES
(N'LoanApplication', CONVERT(NVARCHAR(64), @Application3), N'UPDATE', NULL, N'{"Status":3}', N'credit.officer', DATEADD(HOUR, -5, @Now), N'127.0.0.1', @Now, @Now),
(N'UnderwritingDecision', CONVERT(NVARCHAR(64), @Application4), N'INSERT', NULL, N'{"RecommendedAction":0}', N'under.writer', DATEADD(HOUR, -2, @Now), N'127.0.0.1', @Now, @Now);
GO