/* ============================================================
   RaceDay - Database Schema (Run / Walk / Cycle events)
   Target: SQL Server (SSMS)
   Matches: raceday_erd.png and the EF Core code-first model in
            Part2_RaceDayApi/RaceDay.Api
   Entities: User, Event, Category, EventEnrolment, Result
   Part 3 additions: User.ProfilePictureBlob, Event.BannerBlob, EventEnrolment.Status
   Seed users all use the password: Password123!
   ============================================================ */

IF DB_ID('RaceDayDb') IS NULL
BEGIN
    CREATE DATABASE RaceDayDb;
END
GO

USE RaceDayDb;
GO

/* Drop in FK-safe order if re-running */
IF OBJECT_ID('dbo.Result', 'U') IS NOT NULL DROP TABLE dbo.Result;
IF OBJECT_ID('dbo.EventEnrolment', 'U') IS NOT NULL DROP TABLE dbo.EventEnrolment;
IF OBJECT_ID('dbo.Category', 'U') IS NOT NULL DROP TABLE dbo.Category;
IF OBJECT_ID('dbo.Event', 'U') IS NOT NULL DROP TABLE dbo.Event;
IF OBJECT_ID('dbo.[User]', 'U') IS NOT NULL DROP TABLE dbo.[User];
GO

/* ------------------------------------------------------------
   1. USER - Organisers and Participants (role-based)
   PasswordHash holds a salted PBKDF2 hash, never the password.
   ------------------------------------------------------------ */
CREATE TABLE dbo.[User] (
    UserId        INT IDENTITY(1,1) PRIMARY KEY,
    FullName      NVARCHAR(100) NOT NULL,
    Email         NVARCHAR(150) NOT NULL,
    PasswordHash  NVARCHAR(256) NOT NULL,
    Role          NVARCHAR(20)  NOT NULL,
    Phone         NVARCHAR(20)  NULL,
    DateOfBirth   DATE          NULL,
    CreatedAt     DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    ProfilePictureBlob NVARCHAR(200) NULL,   -- Part 3: blob name in Azure Blob Storage
    CONSTRAINT UQ_User_Email UNIQUE (Email),
    CONSTRAINT CK_User_Role CHECK (Role IN ('Organiser','Participant'))
);
GO

/* ------------------------------------------------------------
   2. EVENT - a run, walk or cycle created by an Organiser
   ------------------------------------------------------------ */
CREATE TABLE dbo.Event (
    EventId      INT IDENTITY(1,1) PRIMARY KEY,
    OrganiserId  INT           NOT NULL,
    Name         NVARCHAR(100) NOT NULL,
    Description  NVARCHAR(1000) NOT NULL,
    EventDate    DATETIME2     NOT NULL,
    Location     NVARCHAR(150) NOT NULL,
    DistanceKm   DECIMAL(6,2)  NOT NULL,
    EventType    NVARCHAR(10)  NOT NULL,
    CreatedAt    DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    BannerBlob   NVARCHAR(200) NULL,         -- Part 3: blob name in Azure Blob Storage
    CONSTRAINT FK_Event_Organiser FOREIGN KEY (OrganiserId) REFERENCES dbo.[User](UserId),
    CONSTRAINT CK_Event_Type CHECK (EventType IN ('Run','Walk','Cycle')),
    CONSTRAINT CK_Event_Distance CHECK (DistanceKm > 0)
);
GO

/* ------------------------------------------------------------
   3. CATEGORY - age or distance category within an event
      Age      -> MinAge and/or MaxAge, no DistanceKm
      Distance -> DistanceKm, no ages
   ------------------------------------------------------------ */
CREATE TABLE dbo.Category (
    CategoryId    INT IDENTITY(1,1) PRIMARY KEY,
    EventId       INT           NOT NULL,
    Name          NVARCHAR(50)  NOT NULL,
    Description   NVARCHAR(255) NULL,
    CategoryType  NVARCHAR(10)  NOT NULL,
    MinAge        INT           NULL,
    MaxAge        INT           NULL,
    DistanceKm    DECIMAL(6,2)  NULL,
    CONSTRAINT FK_Category_Event FOREIGN KEY (EventId) REFERENCES dbo.Event(EventId) ON DELETE CASCADE,
    CONSTRAINT CK_Category_Type CHECK (CategoryType IN ('Age','Distance')),
    CONSTRAINT CK_Category_Rules CHECK (
        (CategoryType = 'Age' AND (MinAge IS NOT NULL OR MaxAge IS NOT NULL) AND DistanceKm IS NULL)
        OR
        (CategoryType = 'Distance' AND DistanceKm IS NOT NULL AND MinAge IS NULL AND MaxAge IS NULL)
    ),
    CONSTRAINT CK_Category_AgeRange CHECK (MinAge IS NULL OR MaxAge IS NULL OR MinAge <= MaxAge)
);
GO

/* ------------------------------------------------------------
   4. EVENTENROLMENT - links Participant + Event + Category
      A participant can enter an event once.
   ------------------------------------------------------------ */
CREATE TABLE dbo.EventEnrolment (
    EnrolmentId    INT IDENTITY(1,1) PRIMARY KEY,
    EventId        INT       NOT NULL,
    ParticipantId  INT       NOT NULL,
    CategoryId     INT       NOT NULL,
    EnrolmentDate  DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    Status         NVARCHAR(10) NOT NULL DEFAULT 'Pending',   -- Part 3: Pending / Confirmed
    CONSTRAINT CK_Enrolment_Status CHECK (Status IN ('Pending','Confirmed')),
    CONSTRAINT FK_Enrolment_Event       FOREIGN KEY (EventId)       REFERENCES dbo.Event(EventId),
    CONSTRAINT FK_Enrolment_Participant FOREIGN KEY (ParticipantId) REFERENCES dbo.[User](UserId),
    CONSTRAINT FK_Enrolment_Category    FOREIGN KEY (CategoryId)    REFERENCES dbo.Category(CategoryId),
    CONSTRAINT UQ_Enrolment_EventParticipant UNIQUE (EventId, ParticipantId)
);
GO

/* ------------------------------------------------------------
   5. RESULT - finish time and position, one per enrolment (1:0..1)
   ------------------------------------------------------------ */
CREATE TABLE dbo.Result (
    ResultId        INT IDENTITY(1,1) PRIMARY KEY,
    EnrolmentId     INT       NOT NULL,
    FinishTime      TIME(0)   NOT NULL,
    FinishPosition  INT       NOT NULL,
    RecordedAt      DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Result_Enrolment FOREIGN KEY (EnrolmentId) REFERENCES dbo.EventEnrolment(EnrolmentId) ON DELETE CASCADE,
    CONSTRAINT UQ_Result_Enrolment UNIQUE (EnrolmentId),
    CONSTRAINT CK_Result_Position CHECK (FinishPosition >= 1)
);
GO

/* ============================================================
   SEED DATA
   2 Organisers, 2 Participants, 3 events (run, walk, cycle),
   age and distance categories, enrolments and results.
   ============================================================ */

INSERT INTO dbo.[User] (FullName, Email, PasswordHash, Role, Phone, DateOfBirth) VALUES
('Lerato Mokoena', 'lerato.organiser@raceday.co.za', 'v1.100000.AQEBAQEBAQEBAQEBAQEBAQ==.rl54YYx+D5xE0GlLh9DTAWEUjfFRwV1DcU06MW8R/+c=', 'Organiser',   '0821234567', NULL),
('Riaan Botha',    'riaan.organiser@raceday.co.za',  'v1.100000.AgICAgICAgICAgICAgICAg==.ziiVYbb5GzQHqX8oLbq1iGlJCQIJUBWcERwuhFHlzWY=', 'Organiser',   '0837654321', NULL),
('Sipho Nkosi',    'sipho.runner@raceday.co.za',     'v1.100000.AwMDAwMDAwMDAwMDAwMDAw==.SUDGS4dxV1tzP/T4bYe9izgqJB3xut30ufWcyhtYt0A=', 'Participant', '0731112222', '1998-03-14'),
('Amy Fischer',    'amy.runner@raceday.co.za',       'v1.100000.BAQEBAQEBAQEBAQEBAQEBA==.ox01PW1wUd9IlHYPFPMtIVdQZfsymFrMHypkcymg99E=', 'Participant', '0745556666', '2007-09-02');
GO

INSERT INTO dbo.Event (OrganiserId, Name, Description, EventDate, Location, DistanceKm, EventType) VALUES
(1, 'Table Mountain Trail Run',  'Scenic trail run along the lower slopes of Table Mountain.', '2026-11-14 07:00:00', 'Cape Town, Western Cape', 21.10, 'Run'),
(1, 'Sea Point Family Walk',     'Relaxed family walk along the Sea Point promenade.',         '2026-11-28 08:00:00', 'Sea Point, Cape Town',     5.00, 'Walk'),
(2, 'Winelands Cycle Classic',   'Road cycle through the Stellenbosch winelands.',             '2026-12-05 06:30:00', 'Stellenbosch, Western Cape', 60.00, 'Cycle');
GO

INSERT INTO dbo.Category (EventId, Name, Description, CategoryType, MinAge, MaxAge, DistanceKm) VALUES
(1, 'Under 20', 'Runners aged 19 and under',  'Age',      NULL, 19,   NULL),
(1, 'Senior',   'Runners aged 20 and over',   'Age',      20,   NULL, NULL),
(2, '5km',      'Standard family walk',       'Distance', NULL, NULL, 5.00),
(3, '60km',     'Full classic route',         'Distance', NULL, NULL, 60.00),
(3, '30km',     'Short route',                'Distance', NULL, NULL, 30.00);
GO

-- Sipho (3) -> Senior; Amy (4) -> Under 20 at the trail run; Sipho -> 60km cycle
INSERT INTO dbo.EventEnrolment (EventId, ParticipantId, CategoryId, Status) VALUES
(1, 3, 2, 'Confirmed'),
(1, 4, 1, 'Confirmed'),
(3, 3, 4, 'Pending');
GO

INSERT INTO dbo.Result (EnrolmentId, FinishTime, FinishPosition) VALUES
(1, '01:42:18', 1),
(2, '01:55:40', 1);
GO
