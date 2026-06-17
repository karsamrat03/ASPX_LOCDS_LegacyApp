SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'locds')
BEGIN
    EXEC('CREATE SCHEMA locds');
END
GO

/*
  NOTE:
  - Primary keys are UNIQUEIDENTIFIER with NEWSEQUENTIALID().
  - Enum columns are stored as TINYINT/INT with check constraints.
*/

IF OBJECT_ID('locds.trg_LoanApplication_Audit', 'TR') IS NOT NULL
    DROP TRIGGER locds.trg_LoanApplication_Audit;
GO

IF OBJECT_ID('locds.usp_SaveUnderwritingDecision', 'P') IS NOT NULL
    DROP PROCEDURE locds.usp_SaveUnderwritingDecision;
IF OBJECT_ID('locds.usp_GetApplicationDetail', 'P') IS NOT NULL
    DROP PROCEDURE locds.usp_GetApplicationDetail;
IF OBJECT_ID('locds.usp_GetApplicationDashboard', 'P') IS NOT NULL
    DROP PROCEDURE locds.usp_GetApplicationDashboard;
GO

IF OBJECT_ID('locds.AuditLog', 'U') IS NOT NULL DROP TABLE locds.AuditLog;
IF OBJECT_ID('locds.LoanOffer', 'U') IS NOT NULL DROP TABLE locds.LoanOffer;
IF OBJECT_ID('locds.UnderwritingDecision', 'U') IS NOT NULL DROP TABLE locds.UnderwritingDecision;
IF OBJECT_ID('locds.CreditBureauReport', 'U') IS NOT NULL DROP TABLE locds.CreditBureauReport;
IF OBJECT_ID('locds.LoanApplication', 'U') IS NOT NULL DROP TABLE locds.LoanApplication;
IF OBJECT_ID('locds.Applicant', 'U') IS NOT NULL DROP TABLE locds.Applicant;
GO

CREATE TABLE locds.Applicant
(
    ApplicantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Applicant_ApplicantId DEFAULT (NEWSEQUENTIALID()),
    FirstName NVARCHAR(100) NOT NULL,
    LastName NVARCHAR(100) NOT NULL,
    DOB DATE NOT NULL,
    PAN NVARCHAR(20) NOT NULL,
    AadhaarHash NVARCHAR(256) NOT NULL,
    AnnualIncome DECIMAL(18,2) NOT NULL,
    EmploymentType TINYINT NOT NULL,
    CreditScore INT NOT NULL,
    RiskTier TINYINT NOT NULL,
    CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_Applicant_CreatedDate DEFAULT (SYSUTCDATETIME()),
    LastModifiedDate DATETIME2(0) NOT NULL CONSTRAINT DF_Applicant_LastModifiedDate DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Applicant PRIMARY KEY CLUSTERED (ApplicantId),
    CONSTRAINT UQ_Applicant_PAN UNIQUE (PAN),
    CONSTRAINT CK_Applicant_EmploymentType CHECK (EmploymentType BETWEEN 0 AND 3),
    CONSTRAINT CK_Applicant_RiskTier CHECK (RiskTier BETWEEN 0 AND 3),
    CONSTRAINT CK_Applicant_CreditScore CHECK (CreditScore BETWEEN 300 AND 900),
    CONSTRAINT CK_Applicant_AnnualIncome CHECK (AnnualIncome >= 0)
);
GO

CREATE TABLE locds.LoanApplication
(
    ApplicationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_LoanApplication_ApplicationId DEFAULT (NEWSEQUENTIALID()),
    ApplicantId UNIQUEIDENTIFIER NOT NULL,
    LoanAmount DECIMAL(18,2) NOT NULL,
    Tenure INT NOT NULL,
    Purpose NVARCHAR(50) NOT NULL,
    Status TINYINT NOT NULL,
    CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_LoanApplication_CreatedDate DEFAULT (SYSUTCDATETIME()),
    LastModifiedDate DATETIME2(0) NOT NULL CONSTRAINT DF_LoanApplication_LastModifiedDate DEFAULT (SYSUTCDATETIME()),
    AssignedUnderwriterId UNIQUEIDENTIFIER NULL,
    CONSTRAINT PK_LoanApplication PRIMARY KEY CLUSTERED (ApplicationId),
    CONSTRAINT FK_LoanApplication_Applicant FOREIGN KEY (ApplicantId)
        REFERENCES locds.Applicant(ApplicantId)
        ON UPDATE CASCADE
        ON DELETE NO ACTION,
    CONSTRAINT CK_LoanApplication_Status CHECK (Status BETWEEN 0 AND 8),
    CONSTRAINT CK_LoanApplication_LoanAmount CHECK (LoanAmount > 0),
    CONSTRAINT CK_LoanApplication_Tenure CHECK (Tenure > 0)
);
GO

CREATE TABLE locds.CreditBureauReport
(
    ReportId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CreditBureauReport_ReportId DEFAULT (NEWSEQUENTIALID()),
    ApplicationId UNIQUEIDENTIFIER NOT NULL,
    Bureau TINYINT NOT NULL,
    Score INT NOT NULL,
    Enquiries INT NOT NULL,
    ActiveLoans INT NOT NULL,
    DefaultHistory BIT NOT NULL,
    PulledAt DATETIME2(0) NOT NULL,
    CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_CreditBureauReport_CreatedDate DEFAULT (SYSUTCDATETIME()),
    LastModifiedDate DATETIME2(0) NOT NULL CONSTRAINT DF_CreditBureauReport_LastModifiedDate DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_CreditBureauReport PRIMARY KEY CLUSTERED (ReportId),
    CONSTRAINT FK_CreditBureauReport_LoanApplication FOREIGN KEY (ApplicationId)
        REFERENCES locds.LoanApplication(ApplicationId)
        ON UPDATE CASCADE
        ON DELETE CASCADE,
    CONSTRAINT CK_CreditBureauReport_Bureau CHECK (Bureau BETWEEN 0 AND 2),
    CONSTRAINT CK_CreditBureauReport_Score CHECK (Score BETWEEN 300 AND 900),
    CONSTRAINT CK_CreditBureauReport_Enquiries CHECK (Enquiries >= 0),
    CONSTRAINT CK_CreditBureauReport_ActiveLoans CHECK (ActiveLoans >= 0)
);
GO

CREATE TABLE locds.UnderwritingDecision
(
    DecisionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_UnderwritingDecision_DecisionId DEFAULT (NEWSEQUENTIALID()),
    ApplicationId UNIQUEIDENTIFIER NOT NULL,
    RecommendedAction TINYINT NOT NULL,
    ApprovedAmount DECIMAL(18,2) NOT NULL,
    InterestRate DECIMAL(9,4) NOT NULL,
    Tenure INT NOT NULL,
    RiskScore DECIMAL(9,4) NOT NULL,
    Remarks NVARCHAR(1000) NULL,
    DecidedBy NVARCHAR(100) NOT NULL,
    DecidedAt DATETIME2(0) NOT NULL,
    CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_UnderwritingDecision_CreatedDate DEFAULT (SYSUTCDATETIME()),
    LastModifiedDate DATETIME2(0) NOT NULL CONSTRAINT DF_UnderwritingDecision_LastModifiedDate DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_UnderwritingDecision PRIMARY KEY CLUSTERED (DecisionId),
    CONSTRAINT UQ_UnderwritingDecision_Application UNIQUE (ApplicationId),
    CONSTRAINT FK_UnderwritingDecision_LoanApplication FOREIGN KEY (ApplicationId)
        REFERENCES locds.LoanApplication(ApplicationId)
        ON UPDATE CASCADE
        ON DELETE CASCADE,
    CONSTRAINT CK_UnderwritingDecision_RecommendedAction CHECK (RecommendedAction BETWEEN 0 AND 3),
    CONSTRAINT CK_UnderwritingDecision_ApprovedAmount CHECK (ApprovedAmount >= 0),
    CONSTRAINT CK_UnderwritingDecision_InterestRate CHECK (InterestRate >= 0),
    CONSTRAINT CK_UnderwritingDecision_Tenure CHECK (Tenure > 0),
    CONSTRAINT CK_UnderwritingDecision_RiskScore CHECK (RiskScore >= 0)
);
GO

CREATE TABLE locds.LoanOffer
(
    OfferId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_LoanOffer_OfferId DEFAULT (NEWSEQUENTIALID()),
    ApplicationId UNIQUEIDENTIFIER NOT NULL,
    EMI DECIMAL(18,2) NOT NULL,
    ProcessingFee DECIMAL(18,2) NOT NULL,
    TotalCost DECIMAL(18,2) NOT NULL,
    ValidUntil DATETIME2(0) NOT NULL,
    IsAccepted BIT NOT NULL CONSTRAINT DF_LoanOffer_IsAccepted DEFAULT (0),
    CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_LoanOffer_CreatedDate DEFAULT (SYSUTCDATETIME()),
    LastModifiedDate DATETIME2(0) NOT NULL CONSTRAINT DF_LoanOffer_LastModifiedDate DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_LoanOffer PRIMARY KEY CLUSTERED (OfferId),
    CONSTRAINT FK_LoanOffer_LoanApplication FOREIGN KEY (ApplicationId)
        REFERENCES locds.LoanApplication(ApplicationId)
        ON UPDATE CASCADE
        ON DELETE CASCADE,
    CONSTRAINT CK_LoanOffer_EMI CHECK (EMI >= 0),
    CONSTRAINT CK_LoanOffer_ProcessingFee CHECK (ProcessingFee >= 0),
    CONSTRAINT CK_LoanOffer_TotalCost CHECK (TotalCost >= 0)
);
GO

CREATE TABLE locds.AuditLog
(
    LogId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_AuditLog_LogId DEFAULT (NEWSEQUENTIALID()),
    EntityType NVARCHAR(100) NOT NULL,
    EntityId NVARCHAR(64) NOT NULL,
    Action NVARCHAR(20) NOT NULL,
    OldValue NVARCHAR(MAX) NULL,
    NewValue NVARCHAR(MAX) NULL,
    PerformedBy NVARCHAR(100) NOT NULL,
    PerformedAt DATETIME2(0) NOT NULL CONSTRAINT DF_AuditLog_PerformedAt DEFAULT (SYSUTCDATETIME()),
    IPAddress NVARCHAR(45) NULL,
    CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_AuditLog_CreatedDate DEFAULT (SYSUTCDATETIME()),
    LastModifiedDate DATETIME2(0) NOT NULL CONSTRAINT DF_AuditLog_LastModifiedDate DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_AuditLog PRIMARY KEY CLUSTERED (LogId)
);
GO

/* Required index coverage */
CREATE NONCLUSTERED INDEX IX_LoanApplication_ApplicationId ON locds.LoanApplication(ApplicationId);
CREATE NONCLUSTERED INDEX IX_LoanApplication_ApplicantId ON locds.LoanApplication(ApplicantId);
CREATE NONCLUSTERED INDEX IX_LoanApplication_Status ON locds.LoanApplication(Status);
CREATE NONCLUSTERED INDEX IX_LoanApplication_CreatedDate ON locds.LoanApplication(CreatedDate DESC);

CREATE NONCLUSTERED INDEX IX_CreditBureauReport_ApplicationId ON locds.CreditBureauReport(ApplicationId);
CREATE NONCLUSTERED INDEX IX_CreditBureauReport_CreatedDate ON locds.CreditBureauReport(CreatedDate DESC);

CREATE NONCLUSTERED INDEX IX_UnderwritingDecision_ApplicationId ON locds.UnderwritingDecision(ApplicationId);
CREATE NONCLUSTERED INDEX IX_UnderwritingDecision_CreatedDate ON locds.UnderwritingDecision(CreatedDate DESC);

CREATE NONCLUSTERED INDEX IX_LoanOffer_ApplicationId ON locds.LoanOffer(ApplicationId);
CREATE NONCLUSTERED INDEX IX_LoanOffer_CreatedDate ON locds.LoanOffer(CreatedDate DESC);

CREATE NONCLUSTERED INDEX IX_Applicant_ApplicantId ON locds.Applicant(ApplicantId);
CREATE NONCLUSTERED INDEX IX_Applicant_CreatedDate ON locds.Applicant(CreatedDate DESC);
GO

/* LoanApplication row-level audit trigger */
CREATE TRIGGER locds.trg_LoanApplication_Audit
ON locds.LoanApplication
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @PerformedBy NVARCHAR(100) = COALESCE(CAST(SESSION_CONTEXT(N'UserName') AS NVARCHAR(100)), SUSER_SNAME(), N'SYSTEM');
    DECLARE @IPAddress NVARCHAR(45) = CAST(SESSION_CONTEXT(N'IPAddress') AS NVARCHAR(45));

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
    SELECT
        N'LoanApplication',
        CONVERT(NVARCHAR(64), COALESCE(i.ApplicationId, d.ApplicationId)),
        CASE
            WHEN d.ApplicationId IS NULL THEN N'INSERT'
            WHEN i.ApplicationId IS NULL THEN N'DELETE'
            ELSE N'UPDATE'
        END,
        CASE WHEN d.ApplicationId IS NULL THEN NULL
             ELSE (
                SELECT d.* FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
             )
        END,
        CASE WHEN i.ApplicationId IS NULL THEN NULL
             ELSE (
                SELECT i.* FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
             )
        END,
        @PerformedBy,
        SYSUTCDATETIME(),
        @IPAddress,
        SYSUTCDATETIME(),
        SYSUTCDATETIME()
    FROM inserted i
    FULL OUTER JOIN deleted d ON i.ApplicationId = d.ApplicationId;
END;
GO

/* 1) Dashboard: paged list with filters */
CREATE PROCEDURE locds.usp_GetApplicationDashboard
    @PageNumber INT = 1,
    @PageSize INT = 25,
    @Status TINYINT = NULL,
    @ApplicantId UNIQUEIDENTIFIER = NULL,
    @RiskTier TINYINT = NULL,
    @FromCreatedDate DATETIME2(0) = NULL,
    @ToCreatedDate DATETIME2(0) = NULL,
    @SearchText NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @PageNumber < 1 SET @PageNumber = 1;
    IF @PageSize < 1 SET @PageSize = 25;

    ;WITH AppRows AS
    (
        SELECT
            LA.ApplicationId,
            LA.ApplicantId,
            A.FirstName,
            A.LastName,
            LA.LoanAmount,
            LA.Tenure,
            LA.Purpose,
            LA.Status,
            LA.CreatedDate,
            A.CreditScore,
            A.RiskTier,
            ROW_NUMBER() OVER (ORDER BY LA.CreatedDate DESC, LA.ApplicationId DESC) AS RowNum,
            COUNT(1) OVER () AS TotalCount
        FROM locds.LoanApplication LA
        INNER JOIN locds.Applicant A ON A.ApplicantId = LA.ApplicantId
        WHERE (@Status IS NULL OR LA.Status = @Status)
          AND (@ApplicantId IS NULL OR LA.ApplicantId = @ApplicantId)
          AND (@RiskTier IS NULL OR A.RiskTier = @RiskTier)
          AND (@FromCreatedDate IS NULL OR LA.CreatedDate >= @FromCreatedDate)
          AND (@ToCreatedDate IS NULL OR LA.CreatedDate < DATEADD(DAY, 1, @ToCreatedDate))
          AND (
                @SearchText IS NULL
                OR A.FirstName LIKE N'%' + @SearchText + N'%'
                OR A.LastName LIKE N'%' + @SearchText + N'%'
                OR A.PAN LIKE N'%' + @SearchText + N'%'
              )
    )
    SELECT
        ApplicationId,
        ApplicantId,
        FirstName,
        LastName,
        LoanAmount,
        Tenure,
        Purpose,
        Status,
        CreatedDate,
        CreditScore,
        RiskTier,
        TotalCount
    FROM AppRows
    WHERE RowNum BETWEEN ((@PageNumber - 1) * @PageSize + 1) AND (@PageNumber * @PageSize)
    ORDER BY RowNum;
END;
GO

/* 2) Full detail: joins across all entity tables */
CREATE PROCEDURE locds.usp_GetApplicationDetail
    @ApplicationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        LA.ApplicationId,
        LA.ApplicantId,
        LA.LoanAmount,
        LA.Tenure,
        LA.Purpose,
        LA.Status,
        LA.CreatedDate,
        LA.LastModifiedDate,
        LA.AssignedUnderwriterId,

        A.FirstName,
        A.LastName,
        A.DOB,
        A.PAN,
        A.AadhaarHash,
        A.AnnualIncome,
        A.EmploymentType,
        A.CreditScore,
        A.RiskTier,

        CBR.ReportId,
        CBR.Bureau,
        CBR.Score,
        CBR.Enquiries,
        CBR.ActiveLoans,
        CBR.DefaultHistory,
        CBR.PulledAt,

        UD.DecisionId,
        UD.RecommendedAction,
        UD.ApprovedAmount,
        UD.InterestRate,
        UD.Tenure AS ApprovedTenure,
        UD.RiskScore,
        UD.Remarks,
        UD.DecidedBy,
        UD.DecidedAt,

        LO.OfferId,
        LO.EMI,
        LO.ProcessingFee,
        LO.TotalCost,
        LO.ValidUntil,
        LO.IsAccepted
    FROM locds.LoanApplication LA
    INNER JOIN locds.Applicant A ON A.ApplicantId = LA.ApplicantId
    LEFT JOIN locds.CreditBureauReport CBR ON CBR.ApplicationId = LA.ApplicationId
    LEFT JOIN locds.UnderwritingDecision UD ON UD.ApplicationId = LA.ApplicationId
    LEFT JOIN locds.LoanOffer LO ON LO.ApplicationId = LA.ApplicationId
    WHERE LA.ApplicationId = @ApplicationId;

    SELECT
        LogId,
        EntityType,
        EntityId,
        Action,
        OldValue,
        NewValue,
        PerformedBy,
        PerformedAt,
        IPAddress
    FROM locds.AuditLog
    WHERE EntityType = N'LoanApplication'
      AND EntityId = CONVERT(NVARCHAR(64), @ApplicationId)
    ORDER BY PerformedAt DESC;
END;
GO

/* 3) Save underwriting decision with transaction */
CREATE PROCEDURE locds.usp_SaveUnderwritingDecision
    @ApplicationId UNIQUEIDENTIFIER,
    @RecommendedAction TINYINT,
    @ApprovedAmount DECIMAL(18,2),
    @InterestRate DECIMAL(9,4),
    @Tenure INT,
    @RiskScore DECIMAL(9,4),
    @Remarks NVARCHAR(1000) = NULL,
    @DecidedBy NVARCHAR(100),
    @DecidedAt DATETIME2(0),
    @UpdatedStatus TINYINT = NULL,
    @AssignedUnderwriterId UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF NOT EXISTS (SELECT 1 FROM locds.LoanApplication WHERE ApplicationId = @ApplicationId)
        BEGIN
            RAISERROR('ApplicationId not found.', 16, 1);
            RETURN;
        END

        IF EXISTS (SELECT 1 FROM locds.UnderwritingDecision WHERE ApplicationId = @ApplicationId)
        BEGIN
            UPDATE locds.UnderwritingDecision
            SET
                RecommendedAction = @RecommendedAction,
                ApprovedAmount = @ApprovedAmount,
                InterestRate = @InterestRate,
                Tenure = @Tenure,
                RiskScore = @RiskScore,
                Remarks = @Remarks,
                DecidedBy = @DecidedBy,
                DecidedAt = @DecidedAt,
                LastModifiedDate = SYSUTCDATETIME()
            WHERE ApplicationId = @ApplicationId;
        END
        ELSE
        BEGIN
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
            (
                @ApplicationId,
                @RecommendedAction,
                @ApprovedAmount,
                @InterestRate,
                @Tenure,
                @RiskScore,
                @Remarks,
                @DecidedBy,
                @DecidedAt,
                SYSUTCDATETIME(),
                SYSUTCDATETIME()
            );
        END

        UPDATE locds.LoanApplication
        SET
            Status = COALESCE(@UpdatedStatus, Status),
            AssignedUnderwriterId = COALESCE(@AssignedUnderwriterId, AssignedUnderwriterId),
            LastModifiedDate = SYSUTCDATETIME()
        WHERE ApplicationId = @ApplicationId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
        DECLARE @ErrorState INT = ERROR_STATE();

        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END;
GO
