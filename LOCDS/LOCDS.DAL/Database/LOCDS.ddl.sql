SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'locds')
BEGIN
    EXEC('CREATE SCHEMA locds');
END
GO

IF OBJECT_ID('locds.LoanPaymentSchedule', 'U') IS NOT NULL DROP TABLE locds.LoanPaymentSchedule;
IF OBJECT_ID('locds.LoanApproval', 'U') IS NOT NULL DROP TABLE locds.LoanApproval;
IF OBJECT_ID('locds.UnderwritingResult', 'U') IS NOT NULL DROP TABLE locds.UnderwritingResult;
IF OBJECT_ID('locds.BureauReport', 'U') IS NOT NULL DROP TABLE locds.BureauReport;
IF OBJECT_ID('locds.ApplicationDecision', 'U') IS NOT NULL DROP TABLE locds.ApplicationDecision;
IF OBJECT_ID('locds.LoanApplication', 'U') IS NOT NULL DROP TABLE locds.LoanApplication;
IF OBJECT_ID('locds.Employer', 'U') IS NOT NULL DROP TABLE locds.Employer;
IF OBJECT_ID('locds.BorrowerAddress', 'U') IS NOT NULL DROP TABLE locds.BorrowerAddress;
IF OBJECT_ID('locds.Borrower', 'U') IS NOT NULL DROP TABLE locds.Borrower;
IF OBJECT_ID('locds.Product', 'U') IS NOT NULL DROP TABLE locds.Product;
IF OBJECT_ID('locds.LoanStatus', 'U') IS NOT NULL DROP TABLE locds.LoanStatus;
IF OBJECT_ID('locds.RiskBand', 'U') IS NOT NULL DROP TABLE locds.RiskBand;
GO

CREATE TABLE locds.RiskBand
(
    RiskBandId TINYINT NOT NULL,
    Name NVARCHAR(20) NOT NULL,
    CONSTRAINT PK_RiskBand PRIMARY KEY CLUSTERED (RiskBandId),
    CONSTRAINT UQ_RiskBand_Name UNIQUE (Name)
);
GO

CREATE TABLE locds.LoanStatus
(
    LoanStatusId TINYINT NOT NULL,
    Name NVARCHAR(30) NOT NULL,
    CONSTRAINT PK_LoanStatus PRIMARY KEY CLUSTERED (LoanStatusId),
    CONSTRAINT UQ_LoanStatus_Name UNIQUE (Name)
);
GO

CREATE TABLE locds.Product
(
    ProductId INT IDENTITY(1,1) NOT NULL,
    ProductCode NVARCHAR(20) NOT NULL,
    ProductName NVARCHAR(100) NOT NULL,
    MinAmount DECIMAL(18,2) NOT NULL,
    MaxAmount DECIMAL(18,2) NOT NULL,
    MinTermMonths SMALLINT NOT NULL,
    MaxTermMonths SMALLINT NOT NULL,
    Active BIT NOT NULL CONSTRAINT DF_Product_Active DEFAULT (1),
    CreatedUtc DATETIME2(0) NOT NULL CONSTRAINT DF_Product_CreatedUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Product PRIMARY KEY CLUSTERED (ProductId),
    CONSTRAINT UQ_Product_ProductCode UNIQUE (ProductCode),
    CONSTRAINT CK_Product_AmountRange CHECK (MinAmount > 0 AND MaxAmount >= MinAmount),
    CONSTRAINT CK_Product_TermRange CHECK (MinTermMonths > 0 AND MaxTermMonths >= MinTermMonths)
);
GO

CREATE TABLE locds.Borrower
(
    BorrowerId BIGINT IDENTITY(1,1) NOT NULL,
    ExternalBorrowerRef UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Borrower_ExternalRef DEFAULT (NEWSEQUENTIALID()),
    FirstName NVARCHAR(100) NOT NULL,
    LastName NVARCHAR(100) NOT NULL,
    DateOfBirth DATE NOT NULL,
    SSN CHAR(11) NOT NULL,
    Email NVARCHAR(256) NULL,
    Phone NVARCHAR(30) NULL,
    CreatedUtc DATETIME2(0) NOT NULL CONSTRAINT DF_Borrower_CreatedUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Borrower PRIMARY KEY CLUSTERED (BorrowerId),
    CONSTRAINT UQ_Borrower_ExternalRef UNIQUE (ExternalBorrowerRef),
    CONSTRAINT UQ_Borrower_SSN UNIQUE (SSN)
);
GO

CREATE TABLE locds.BorrowerAddress
(
    BorrowerAddressId BIGINT IDENTITY(1,1) NOT NULL,
    BorrowerId BIGINT NOT NULL,
    AddressType NVARCHAR(20) NOT NULL,
    Line1 NVARCHAR(200) NOT NULL,
    Line2 NVARCHAR(200) NULL,
    City NVARCHAR(100) NOT NULL,
    [State] NVARCHAR(50) NOT NULL,
    PostalCode NVARCHAR(20) NOT NULL,
    Country NVARCHAR(50) NOT NULL CONSTRAINT DF_BorrowerAddress_Country DEFAULT ('USA'),
    IsPrimary BIT NOT NULL CONSTRAINT DF_BorrowerAddress_IsPrimary DEFAULT (0),
    CreatedUtc DATETIME2(0) NOT NULL CONSTRAINT DF_BorrowerAddress_CreatedUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_BorrowerAddress PRIMARY KEY CLUSTERED (BorrowerAddressId),
    CONSTRAINT FK_BorrowerAddress_Borrower FOREIGN KEY (BorrowerId) REFERENCES locds.Borrower(BorrowerId) ON DELETE CASCADE
);
GO

CREATE TABLE locds.Employer
(
    EmployerId BIGINT IDENTITY(1,1) NOT NULL,
    BorrowerId BIGINT NOT NULL,
    EmployerName NVARCHAR(200) NOT NULL,
    JobTitle NVARCHAR(100) NULL,
    MonthlyIncome DECIMAL(18,2) NOT NULL,
    EmploymentStartDate DATE NULL,
    Active BIT NOT NULL CONSTRAINT DF_Employer_Active DEFAULT (1),
    CreatedUtc DATETIME2(0) NOT NULL CONSTRAINT DF_Employer_CreatedUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Employer PRIMARY KEY CLUSTERED (EmployerId),
    CONSTRAINT FK_Employer_Borrower FOREIGN KEY (BorrowerId) REFERENCES locds.Borrower(BorrowerId) ON DELETE CASCADE,
    CONSTRAINT CK_Employer_MonthlyIncome CHECK (MonthlyIncome >= 0)
);
GO

CREATE TABLE locds.LoanApplication
(
    ApplicationId BIGINT IDENTITY(1,1) NOT NULL,
    ExternalApplicationRef UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_LoanApplication_ExternalRef DEFAULT (NEWSEQUENTIALID()),
    BorrowerId BIGINT NOT NULL,
    ProductId INT NOT NULL,
    LoanStatusId TINYINT NOT NULL,
    SubmittedUtc DATETIME2(0) NULL,
    RequestedAmount DECIMAL(18,2) NOT NULL,
    RequestedTermMonths SMALLINT NOT NULL,
    MonthlyIncome DECIMAL(18,2) NOT NULL,
    MonthlyDebtPayments DECIMAL(18,2) NOT NULL,
    Purpose NVARCHAR(200) NULL,
    CreatedUtc DATETIME2(0) NOT NULL CONSTRAINT DF_LoanApplication_CreatedUtc DEFAULT (SYSUTCDATETIME()),
    UpdatedUtc DATETIME2(0) NOT NULL CONSTRAINT DF_LoanApplication_UpdatedUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_LoanApplication PRIMARY KEY CLUSTERED (ApplicationId),
    CONSTRAINT UQ_LoanApplication_ExternalRef UNIQUE (ExternalApplicationRef),
    CONSTRAINT FK_LoanApplication_Borrower FOREIGN KEY (BorrowerId) REFERENCES locds.Borrower(BorrowerId),
    CONSTRAINT FK_LoanApplication_Product FOREIGN KEY (ProductId) REFERENCES locds.Product(ProductId),
    CONSTRAINT FK_LoanApplication_LoanStatus FOREIGN KEY (LoanStatusId) REFERENCES locds.LoanStatus(LoanStatusId),
    CONSTRAINT CK_LoanApplication_RequestedAmount CHECK (RequestedAmount > 0),
    CONSTRAINT CK_LoanApplication_RequestedTerm CHECK (RequestedTermMonths > 0),
    CONSTRAINT CK_LoanApplication_MonthlyIncome CHECK (MonthlyIncome > 0),
    CONSTRAINT CK_LoanApplication_MonthlyDebt CHECK (MonthlyDebtPayments >= 0)
);
GO

CREATE TABLE locds.BureauReport
(
    BureauReportId BIGINT IDENTITY(1,1) NOT NULL,
    ApplicationId BIGINT NOT NULL,
    Provider NVARCHAR(50) NOT NULL,
    PulledUtc DATETIME2(0) NOT NULL CONSTRAINT DF_BureauReport_PulledUtc DEFAULT (SYSUTCDATETIME()),
    CreditScore SMALLINT NOT NULL,
    LatePaymentsLast12Months SMALLINT NOT NULL,
    HasRecentBankruptcy BIT NOT NULL,
    TotalTradeLines INT NULL,
    DelinquentTradeLines INT NULL,
    RawPayload NVARCHAR(MAX) NULL,
    CONSTRAINT PK_BureauReport PRIMARY KEY CLUSTERED (BureauReportId),
    CONSTRAINT FK_BureauReport_LoanApplication FOREIGN KEY (ApplicationId) REFERENCES locds.LoanApplication(ApplicationId) ON DELETE CASCADE,
    CONSTRAINT CK_BureauReport_CreditScore CHECK (CreditScore BETWEEN 300 AND 850),
    CONSTRAINT CK_BureauReport_LatePayments CHECK (LatePaymentsLast12Months >= 0)
);
GO

CREATE TABLE locds.UnderwritingResult
(
    UnderwritingResultId BIGINT IDENTITY(1,1) NOT NULL,
    ApplicationId BIGINT NOT NULL,
    RiskBandId TINYINT NOT NULL,
    DebtToIncomeRatio DECIMAL(9,6) NOT NULL,
    IsApproved BIT NOT NULL,
    ReasonCode NVARCHAR(50) NULL,
    ReasonText NVARCHAR(500) NULL,
    EvaluatedUtc DATETIME2(0) NOT NULL CONSTRAINT DF_UnderwritingResult_EvaluatedUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_UnderwritingResult PRIMARY KEY CLUSTERED (UnderwritingResultId),
    CONSTRAINT FK_UnderwritingResult_LoanApplication FOREIGN KEY (ApplicationId) REFERENCES locds.LoanApplication(ApplicationId) ON DELETE CASCADE,
    CONSTRAINT FK_UnderwritingResult_RiskBand FOREIGN KEY (RiskBandId) REFERENCES locds.RiskBand(RiskBandId),
    CONSTRAINT CK_UnderwritingResult_DTI CHECK (DebtToIncomeRatio >= 0)
);
GO

CREATE TABLE locds.ApplicationDecision
(
    ApplicationDecisionId BIGINT IDENTITY(1,1) NOT NULL,
    ApplicationId BIGINT NOT NULL,
    DecisionStatus NVARCHAR(20) NOT NULL,
    DecisionReason NVARCHAR(500) NULL,
    DecidedBy NVARCHAR(100) NULL,
    DecidedUtc DATETIME2(0) NOT NULL CONSTRAINT DF_ApplicationDecision_DecidedUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_ApplicationDecision PRIMARY KEY CLUSTERED (ApplicationDecisionId),
    CONSTRAINT FK_ApplicationDecision_LoanApplication FOREIGN KEY (ApplicationId) REFERENCES locds.LoanApplication(ApplicationId) ON DELETE CASCADE
);
GO

CREATE TABLE locds.LoanApproval
(
    LoanApprovalId BIGINT IDENTITY(1,1) NOT NULL,
    ApplicationId BIGINT NOT NULL,
    ApprovedAmount DECIMAL(18,2) NOT NULL,
    ApprovedTermMonths SMALLINT NOT NULL,
    ApprovedRate DECIMAL(9,6) NOT NULL,
    FundingDate DATE NULL,
    CreatedUtc DATETIME2(0) NOT NULL CONSTRAINT DF_LoanApproval_CreatedUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_LoanApproval PRIMARY KEY CLUSTERED (LoanApprovalId),
    CONSTRAINT UQ_LoanApproval_Application UNIQUE (ApplicationId),
    CONSTRAINT FK_LoanApproval_LoanApplication FOREIGN KEY (ApplicationId) REFERENCES locds.LoanApplication(ApplicationId) ON DELETE CASCADE,
    CONSTRAINT CK_LoanApproval_Amount CHECK (ApprovedAmount > 0),
    CONSTRAINT CK_LoanApproval_Term CHECK (ApprovedTermMonths > 0),
    CONSTRAINT CK_LoanApproval_Rate CHECK (ApprovedRate >= 0)
);
GO

CREATE TABLE locds.LoanPaymentSchedule
(
    PaymentScheduleId BIGINT IDENTITY(1,1) NOT NULL,
    LoanApprovalId BIGINT NOT NULL,
    InstallmentNo INT NOT NULL,
    DueDate DATE NOT NULL,
    PrincipalDue DECIMAL(18,2) NOT NULL,
    InterestDue DECIMAL(18,2) NOT NULL,
    FeesDue DECIMAL(18,2) NOT NULL CONSTRAINT DF_LoanPaymentSchedule_FeesDue DEFAULT (0),
    IsPaid BIT NOT NULL CONSTRAINT DF_LoanPaymentSchedule_IsPaid DEFAULT (0),
    PaidUtc DATETIME2(0) NULL,
    CONSTRAINT PK_LoanPaymentSchedule PRIMARY KEY CLUSTERED (PaymentScheduleId),
    CONSTRAINT UQ_LoanPaymentSchedule_Installment UNIQUE (LoanApprovalId, InstallmentNo),
    CONSTRAINT FK_LoanPaymentSchedule_LoanApproval FOREIGN KEY (LoanApprovalId) REFERENCES locds.LoanApproval(LoanApprovalId) ON DELETE CASCADE,
    CONSTRAINT CK_LoanPaymentSchedule_Amounts CHECK (PrincipalDue >= 0 AND InterestDue >= 0 AND FeesDue >= 0)
);
GO

CREATE NONCLUSTERED INDEX IX_Borrower_LastName_FirstName ON locds.Borrower(LastName, FirstName);
CREATE NONCLUSTERED INDEX IX_BorrowerAddress_BorrowerId ON locds.BorrowerAddress(BorrowerId);
CREATE NONCLUSTERED INDEX IX_Employer_BorrowerId_Active ON locds.Employer(BorrowerId, Active);
CREATE NONCLUSTERED INDEX IX_LoanApplication_BorrowerId ON locds.LoanApplication(BorrowerId);
CREATE NONCLUSTERED INDEX IX_LoanApplication_LoanStatusId_SubmittedUtc ON locds.LoanApplication(LoanStatusId, SubmittedUtc);
CREATE NONCLUSTERED INDEX IX_LoanApplication_ProductId ON locds.LoanApplication(ProductId);
CREATE NONCLUSTERED INDEX IX_BureauReport_ApplicationId_PulledUtc ON locds.BureauReport(ApplicationId, PulledUtc DESC);
CREATE NONCLUSTERED INDEX IX_UnderwritingResult_ApplicationId_EvaluatedUtc ON locds.UnderwritingResult(ApplicationId, EvaluatedUtc DESC);
CREATE NONCLUSTERED INDEX IX_ApplicationDecision_ApplicationId_DecidedUtc ON locds.ApplicationDecision(ApplicationId, DecidedUtc DESC);
CREATE NONCLUSTERED INDEX IX_LoanPaymentSchedule_LoanApprovalId_DueDate ON locds.LoanPaymentSchedule(LoanApprovalId, DueDate);
GO

INSERT INTO locds.RiskBand (RiskBandId, Name)
VALUES (1, 'Low'), (2, 'Medium'), (3, 'High');

INSERT INTO locds.LoanStatus (LoanStatusId, Name)
VALUES (1, 'Draft'), (2, 'Submitted'), (3, 'InReview'), (4, 'Approved'), (5, 'Declined'), (6, 'Funded');
GO

IF OBJECT_ID('locds.usp_SubmitLoanApplication', 'P') IS NOT NULL
    DROP PROCEDURE locds.usp_SubmitLoanApplication;
GO

CREATE PROCEDURE locds.usp_SubmitLoanApplication
    @ApplicationId BIGINT,
    @SubmittedUtc DATETIME2(0) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE A
    SET
        A.SubmittedUtc = ISNULL(@SubmittedUtc, SYSUTCDATETIME()),
        A.LoanStatusId = 2,
        A.UpdatedUtc = SYSUTCDATETIME()
    FROM locds.LoanApplication A
    WHERE A.ApplicationId = @ApplicationId;

    IF @@ROWCOUNT = 0
    BEGIN
        THROW 51001, 'Application not found.', 1;
    END
END;
GO

IF OBJECT_ID('locds.usp_RecordBureauReport', 'P') IS NOT NULL
    DROP PROCEDURE locds.usp_RecordBureauReport;
GO

CREATE PROCEDURE locds.usp_RecordBureauReport
    @ApplicationId BIGINT,
    @Provider NVARCHAR(50),
    @CreditScore SMALLINT,
    @LatePaymentsLast12Months SMALLINT,
    @HasRecentBankruptcy BIT,
    @TotalTradeLines INT = NULL,
    @DelinquentTradeLines INT = NULL,
    @RawPayload NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM locds.LoanApplication WHERE ApplicationId = @ApplicationId)
    BEGIN
        THROW 51002, 'Application not found for bureau report.', 1;
    END

    INSERT INTO locds.BureauReport
    (
        ApplicationId,
        Provider,
        CreditScore,
        LatePaymentsLast12Months,
        HasRecentBankruptcy,
        TotalTradeLines,
        DelinquentTradeLines,
        RawPayload
    )
    VALUES
    (
        @ApplicationId,
        @Provider,
        @CreditScore,
        @LatePaymentsLast12Months,
        @HasRecentBankruptcy,
        @TotalTradeLines,
        @DelinquentTradeLines,
        @RawPayload
    );
END;
GO

IF OBJECT_ID('locds.usp_RunUnderwritingDecision', 'P') IS NOT NULL
    DROP PROCEDURE locds.usp_RunUnderwritingDecision;
GO

CREATE PROCEDURE locds.usp_RunUnderwritingDecision
    @ApplicationId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @MonthlyIncome DECIMAL(18,2);
    DECLARE @MonthlyDebtPayments DECIMAL(18,2);
    DECLARE @CreditScore SMALLINT;
    DECLARE @LatePayments SMALLINT;
    DECLARE @HasRecentBankruptcy BIT;
    DECLARE @DTI DECIMAL(9,6);
    DECLARE @RiskBandId TINYINT;
    DECLARE @IsApproved BIT;
    DECLARE @ReasonCode NVARCHAR(50);
    DECLARE @ReasonText NVARCHAR(500);

    SELECT
        @MonthlyIncome = A.MonthlyIncome,
        @MonthlyDebtPayments = A.MonthlyDebtPayments
    FROM locds.LoanApplication A
    WHERE A.ApplicationId = @ApplicationId;

    IF @MonthlyIncome IS NULL
    BEGIN
        THROW 51003, 'Application not found for underwriting.', 1;
    END

    SELECT TOP (1)
        @CreditScore = B.CreditScore,
        @LatePayments = B.LatePaymentsLast12Months,
        @HasRecentBankruptcy = B.HasRecentBankruptcy
    FROM locds.BureauReport B
    WHERE B.ApplicationId = @ApplicationId
    ORDER BY B.PulledUtc DESC;

    IF @CreditScore IS NULL
    BEGIN
        THROW 51004, 'Bureau report not found for underwriting.', 1;
    END

    SET @DTI = CASE WHEN @MonthlyIncome <= 0 THEN 1 ELSE @MonthlyDebtPayments / @MonthlyIncome END;

    IF (@CreditScore < 600 OR @DTI > 0.55 OR @HasRecentBankruptcy = 1)
    BEGIN
        SET @RiskBandId = 3;
    END
    ELSE IF (@CreditScore < 700 OR @DTI > 0.40 OR @LatePayments > 2)
    BEGIN
        SET @RiskBandId = 2;
    END
    ELSE
    BEGIN
        SET @RiskBandId = 1;
    END

    SET @IsApproved = CASE WHEN @RiskBandId = 3 OR @HasRecentBankruptcy = 1 THEN 0 ELSE 1 END;
    SET @ReasonCode = CASE WHEN @IsApproved = 1 THEN 'APPROVED' ELSE 'DECLINED' END;
    SET @ReasonText = CASE WHEN @IsApproved = 1 THEN 'Application meets underwriting policy.' ELSE 'Application exceeds underwriting policy.' END;

    INSERT INTO locds.UnderwritingResult
    (
        ApplicationId,
        RiskBandId,
        DebtToIncomeRatio,
        IsApproved,
        ReasonCode,
        ReasonText
    )
    VALUES
    (
        @ApplicationId,
        @RiskBandId,
        @DTI,
        @IsApproved,
        @ReasonCode,
        @ReasonText
    );

    INSERT INTO locds.ApplicationDecision
    (
        ApplicationId,
        DecisionStatus,
        DecisionReason,
        DecidedBy
    )
    VALUES
    (
        @ApplicationId,
        CASE WHEN @IsApproved = 1 THEN 'Approved' ELSE 'Declined' END,
        @ReasonText,
        'SYSTEM'
    );

    UPDATE locds.LoanApplication
    SET
        LoanStatusId = CASE WHEN @IsApproved = 1 THEN 4 ELSE 5 END,
        UpdatedUtc = SYSUTCDATETIME()
    WHERE ApplicationId = @ApplicationId;

    SELECT
        @ApplicationId AS ApplicationId,
        @RiskBandId AS RiskBandId,
        @DTI AS DebtToIncomeRatio,
        @IsApproved AS IsApproved,
        @ReasonCode AS ReasonCode,
        @ReasonText AS ReasonText;
END;
GO
