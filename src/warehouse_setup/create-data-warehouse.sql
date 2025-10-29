-- Gold Layer Data Warehouse Schema
-- Coffee Shop Analytics Data Warehouse

-- ============================================================================
-- CLEANUP - DROP EXISTING TABLES
-- ============================================================================

-- Drop fact tables first (due to foreign key dependencies)
DROP TABLE IF EXISTS FactSalesLineItems;
DROP TABLE IF EXISTS FactRecommendation;
DROP TABLE IF EXISTS FactLoyalty;
DROP TABLE IF EXISTS FactSales;

-- Drop dimension tables
DROP TABLE IF EXISTS DimTime;
DROP TABLE IF EXISTS DimDate;
DROP TABLE IF EXISTS DimMenuItem;
DROP TABLE IF EXISTS DimShop;
DROP TABLE IF EXISTS DimCustomer;

-- ============================================================================
-- DIMENSION TABLES
-- ============================================================================

-- 1. DimCustomer
CREATE TABLE DimCustomer (
    CustomerKey INT NOT NULL,
    CustomerId VARCHAR(64) NOT NULL,
    CustomerName VARCHAR(128),
    PreferredAirport VARCHAR(32),
    Email VARCHAR(256),
    FavoriteDrink VARCHAR(128),
    RegistrationDate DATE,
    IsActive BIT,
    UpdatedAt DATETIME2(3),
    CreatedAt DATETIME2(3)
);

-- 2. DimShop
CREATE TABLE DimShop (
    ShopKey INT NOT NULL,
    ShopId VARCHAR(64) NOT NULL,
    ShopName VARCHAR(128),
    AirportId VARCHAR(32),
    AirportName VARCHAR(128),
    Terminal VARCHAR(64),
    Timezone VARCHAR(64),
    IsActive BIT,
    CreatedAt DATETIME2(3),
    UpdatedAt DATETIME2(3)
);

-- 3. DimMenuItem
CREATE TABLE DimMenuItem (
    MenuItemKey INT NOT NULL,
    MenuItemId VARCHAR(64) NOT NULL,
    MenuItemName VARCHAR(128),
    Category VARCHAR(64),
    Price DECIMAL(10,2),
    IsRecommended BIT,
    Calories INT,
    IsActive BIT,
    CreatedAt DATETIME2(3),
    UpdatedAt DATETIME2(3)
);

-- 4. DimDate
CREATE TABLE DimDate (
    DateKey INT NOT NULL,  -- Format: YYYYMMDD (e.g., 20251015)
    FullDate DATE NOT NULL,
    DayOfWeek INT,
    DayName VARCHAR(10),
    MonthNumber INT,
    MonthName VARCHAR(10),
    Quarter INT,
    Year INT,
    IsWeekend BIT,
    IsHoliday BIT
);

-- 5. DimTime
CREATE TABLE DimTime (
    TimeKey INT NOT NULL,  -- Format: HHMMSS (e.g., 143000 = 2:30 PM)
    FullTime TIME(3) NOT NULL,
    Hour INT,
    Hour12 INT,
    AMPM VARCHAR(2),
    TimeOfDay VARCHAR(20), -- Morning, Afternoon, Evening
    BusinessPeriod VARCHAR(20) -- Breakfast, Lunch, Dinner, Late Night
);

-- ============================================================================
-- FACT TABLES
-- ============================================================================

-- 6. FactSales
CREATE TABLE FactSales (
    SalesKey BIGINT NOT NULL,
    TransactionId VARCHAR(64) NOT NULL,
    
    -- Foreign Keys
    DateKey INT NOT NULL,
    TimeKey INT NOT NULL,
    CustomerKey INT NOT NULL,
    ShopKey INT NOT NULL,
    
    -- Measures
    TotalQuantity INT NOT NULL,
    TotalAmount DECIMAL(10,2) NOT NULL,
    
    -- Loyalty
    LoyaltyPointsEarned INT,
    LoyaltyPointsRedeemed INT,
    
    -- Attributes
    PaymentMethod VARCHAR(32),
    
    -- Metadata
    CreatedAt DATETIME2(3)
);

-- 7. FactSalesLineItems
CREATE TABLE FactSalesLineItems (
    TransactionId VARCHAR(64) NOT NULL,
    SalesKey BIGINT NOT NULL,
    LineNumber INT NOT NULL,
    
    -- Foreign Keys
    DateKey INT NOT NULL,
    TimeKey INT NOT NULL,
    MenuItemKey INT NOT NULL,
    
    -- Measures
    Quantity INT NOT NULL,
    UnitPrice DECIMAL(10,2) NOT NULL,
    LineTotal DECIMAL(10,2) NOT NULL,
    
    -- Attributes
    PaymentMethod VARCHAR(32),
    Size VARCHAR(32), -- Small, Medium, Large
    
    -- Metadata
    CreatedAt DATETIME2(3)
);

-- 8. FactLoyalty
CREATE TABLE FactLoyalty (
    LoyaltyKey BIGINT NOT NULL,
    LoyaltyTransactionId VARCHAR(64) NOT NULL,
    
    -- Foreign Keys
    DateKey INT NOT NULL,
    CustomerKey INT NOT NULL,
    ShopKey INT NULL, -- Nullable for non-purchase loyalty events
    
    -- Measures
    PointsChange INT NOT NULL, -- Positive for earning, negative for redemption
    PointsBalance INT NOT NULL,
    
    -- Attributes
    TransactionType VARCHAR(32), -- earning, redemption, bonus, expiration
    RelatedTransactionId VARCHAR(64), -- Link to sales transaction
    
    -- Metadata
    SourceSystem VARCHAR(50),
    CreatedAt DATETIME2(3)
);

-- 9. FactRecommendation
CREATE TABLE FactRecommendation (
    RecommendationKey BIGINT NOT NULL,
    RecommendationId VARCHAR(64) NOT NULL,
    
    -- Foreign Keys
    DateKey INT NOT NULL,
    CustomerKey INT NOT NULL,
    MenuItemKey INT NOT NULL,
    
    -- Measures
    RecommendationScore DECIMAL(5,4), -- ML model score (0-1)
    RecommendationRank INT, -- 1, 2, 3 for top-k
    
    -- Engagement Metrics
    WasDisplayed BIT,
    WasPurchased BIT,
    PurchaseTransactionId VARCHAR(64), -- If purchased
    
    -- Context
    TimeOfDay VARCHAR(32),
    Airport VARCHAR(32),
    
    -- Metadata
    GeneratedAt DATETIME2(3),
    SourceSystem VARCHAR(50),
    CreatedAt DATETIME2(3)
);

-- ============================================================================
-- PRIMARY KEY CONSTRAINTS
-- ============================================================================

ALTER TABLE DimCustomer ADD CONSTRAINT PK_DimCustomer PRIMARY KEY NONCLUSTERED (CustomerKey) NOT ENFORCED;
ALTER TABLE DimShop ADD CONSTRAINT PK_DimShop PRIMARY KEY NONCLUSTERED (ShopKey) NOT ENFORCED;
ALTER TABLE DimMenuItem ADD CONSTRAINT PK_DimMenuItem PRIMARY KEY NONCLUSTERED (MenuItemKey) NOT ENFORCED;
ALTER TABLE DimDate ADD CONSTRAINT PK_DimDate PRIMARY KEY NONCLUSTERED (DateKey) NOT ENFORCED;
ALTER TABLE DimTime ADD CONSTRAINT PK_DimTime PRIMARY KEY NONCLUSTERED (TimeKey) NOT ENFORCED;
ALTER TABLE FactSales ADD CONSTRAINT PK_FactSales PRIMARY KEY NONCLUSTERED (SalesKey) NOT ENFORCED;
ALTER TABLE FactSalesLineItems ADD CONSTRAINT PK_FactSalesLineItems PRIMARY KEY NONCLUSTERED (TransactionId, LineNumber) NOT ENFORCED;
ALTER TABLE FactLoyalty ADD CONSTRAINT PK_FactLoyalty PRIMARY KEY NONCLUSTERED (LoyaltyKey) NOT ENFORCED;
ALTER TABLE FactRecommendation ADD CONSTRAINT PK_FactRecommendation PRIMARY KEY NONCLUSTERED (RecommendationKey) NOT ENFORCED;

-- ============================================================================
-- FOREIGN KEY CONSTRAINTS
-- ============================================================================

ALTER TABLE FactSales ADD CONSTRAINT FK_FactSales_Date FOREIGN KEY (DateKey) REFERENCES DimDate(DateKey) NOT ENFORCED;
ALTER TABLE FactSales ADD CONSTRAINT FK_FactSales_Time FOREIGN KEY (TimeKey) REFERENCES DimTime(TimeKey) NOT ENFORCED;
ALTER TABLE FactSales ADD CONSTRAINT FK_FactSales_Customer FOREIGN KEY (CustomerKey) REFERENCES DimCustomer(CustomerKey) NOT ENFORCED;
ALTER TABLE FactSales ADD CONSTRAINT FK_FactSales_Shop FOREIGN KEY (ShopKey) REFERENCES DimShop(ShopKey) NOT ENFORCED;

ALTER TABLE FactSalesLineItems ADD CONSTRAINT FK_FactSalesLineItems_Date FOREIGN KEY (DateKey) REFERENCES DimDate(DateKey) NOT ENFORCED;
ALTER TABLE FactSalesLineItems ADD CONSTRAINT FK_FactSalesLineItems_Time FOREIGN KEY (TimeKey) REFERENCES DimTime(TimeKey) NOT ENFORCED;
ALTER TABLE FactSalesLineItems ADD CONSTRAINT FK_FactSalesLineItems_MenuItem FOREIGN KEY (MenuItemKey) REFERENCES DimMenuItem(MenuItemKey) NOT ENFORCED;
ALTER TABLE FactSalesLineItems ADD CONSTRAINT FK_FactSalesLineItems_Sales FOREIGN KEY (SalesKey) REFERENCES FactSales(SalesKey) NOT ENFORCED;

ALTER TABLE FactLoyalty ADD CONSTRAINT FK_FactLoyalty_Date FOREIGN KEY (DateKey) REFERENCES DimDate(DateKey) NOT ENFORCED;
ALTER TABLE FactLoyalty ADD CONSTRAINT FK_FactLoyalty_Customer FOREIGN KEY (CustomerKey) REFERENCES DimCustomer(CustomerKey) NOT ENFORCED;
ALTER TABLE FactLoyalty ADD CONSTRAINT FK_FactLoyalty_Shop FOREIGN KEY (ShopKey) REFERENCES DimShop(ShopKey) NOT ENFORCED;

ALTER TABLE FactRecommendation ADD CONSTRAINT FK_FactRecommendation_Date FOREIGN KEY (DateKey) REFERENCES DimDate(DateKey) NOT ENFORCED;
ALTER TABLE FactRecommendation ADD CONSTRAINT FK_FactRecommendation_Customer FOREIGN KEY (CustomerKey) REFERENCES DimCustomer(CustomerKey) NOT ENFORCED;
ALTER TABLE FactRecommendation ADD CONSTRAINT FK_FactRecommendation_MenuItem FOREIGN KEY (MenuItemKey) REFERENCES DimMenuItem(MenuItemKey) NOT ENFORCED;

-- ============================================================================
-- UNIQUE CONSTRAINTS
-- ============================================================================

ALTER TABLE DimCustomer ADD CONSTRAINT UQ_DimCustomer_CustomerId UNIQUE NONCLUSTERED (CustomerId) NOT ENFORCED;
ALTER TABLE DimShop ADD CONSTRAINT UQ_DimShop_ShopId UNIQUE NONCLUSTERED (ShopId) NOT ENFORCED;
ALTER TABLE DimMenuItem ADD CONSTRAINT UQ_DimMenuItem_MenuItemId UNIQUE NONCLUSTERED (MenuItemId) NOT ENFORCED;
ALTER TABLE DimDate ADD CONSTRAINT UQ_DimDate_FullDate UNIQUE NONCLUSTERED (FullDate) NOT ENFORCED;
ALTER TABLE DimTime ADD CONSTRAINT UQ_DimTime_FullTime UNIQUE NONCLUSTERED (FullTime) NOT ENFORCED;
ALTER TABLE FactSales ADD CONSTRAINT UQ_FactSales_TransactionId UNIQUE NONCLUSTERED (TransactionId) NOT ENFORCED;
ALTER TABLE FactLoyalty ADD CONSTRAINT UQ_FactLoyalty_LoyaltyTransactionId UNIQUE NONCLUSTERED (LoyaltyTransactionId) NOT ENFORCED;

-- ============================================================================
-- SUMMARY
-- ============================================================================
-- Tables Created:
-- Dimension Tables (5): DimCustomer, DimShop, DimMenuItem, DimDate, DimTime
-- Fact Tables (4): FactSales, FactSalesLineItems, FactLoyalty, FactRecommendation
-- Total Tables: 9
-- Indexes: Managed automatically by Fabric Data Warehouse
-- Foreign Key Constraints: 8
-- ============================================================================