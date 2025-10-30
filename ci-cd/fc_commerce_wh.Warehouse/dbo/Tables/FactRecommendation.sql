CREATE TABLE [dbo].[FactRecommendation] (

	[RecommendationKey] bigint NOT NULL, 
	[RecommendationId] varchar(64) NOT NULL, 
	[DateKey] int NOT NULL, 
	[CustomerKey] int NOT NULL, 
	[MenuItemKey] int NOT NULL, 
	[RecommendationScore] decimal(5,4) NULL, 
	[RecommendationRank] int NULL, 
	[WasDisplayed] bit NULL, 
	[WasPurchased] bit NULL, 
	[PurchaseTransactionId] varchar(64) NULL, 
	[TimeOfDay] varchar(32) NULL, 
	[Airport] varchar(32) NULL, 
	[GeneratedAt] datetime2(3) NULL, 
	[SourceSystem] varchar(50) NULL, 
	[CreatedAt] datetime2(3) NULL
);


GO
ALTER TABLE [dbo].[FactRecommendation] ADD CONSTRAINT PK_FactRecommendation primary key NONCLUSTERED ([RecommendationKey]);
GO
ALTER TABLE [dbo].[FactRecommendation] ADD CONSTRAINT FK_FactRecommendation_Customer FOREIGN KEY ([CustomerKey]) REFERENCES [dbo].[DimCustomer]([CustomerKey]);
GO
ALTER TABLE [dbo].[FactRecommendation] ADD CONSTRAINT FK_FactRecommendation_Date FOREIGN KEY ([DateKey]) REFERENCES [dbo].[DimDate]([DateKey]);
GO
ALTER TABLE [dbo].[FactRecommendation] ADD CONSTRAINT FK_FactRecommendation_MenuItem FOREIGN KEY ([MenuItemKey]) REFERENCES [dbo].[DimMenuItem]([MenuItemKey]);