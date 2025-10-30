CREATE TABLE [dbo].[FactLoyalty] (

	[LoyaltyKey] bigint NOT NULL, 
	[LoyaltyTransactionId] varchar(64) NOT NULL, 
	[DateKey] int NOT NULL, 
	[CustomerKey] int NOT NULL, 
	[ShopKey] int NULL, 
	[PointsChange] int NOT NULL, 
	[PointsBalance] int NOT NULL, 
	[TransactionType] varchar(32) NULL, 
	[RelatedTransactionId] varchar(64) NULL, 
	[SourceSystem] varchar(50) NULL, 
	[CreatedAt] datetime2(3) NULL
);


GO
ALTER TABLE [dbo].[FactLoyalty] ADD CONSTRAINT PK_FactLoyalty primary key NONCLUSTERED ([LoyaltyKey]);
GO
ALTER TABLE [dbo].[FactLoyalty] ADD CONSTRAINT UQ_FactLoyalty_LoyaltyTransactionId unique NONCLUSTERED ([LoyaltyTransactionId]);
GO
ALTER TABLE [dbo].[FactLoyalty] ADD CONSTRAINT FK_FactLoyalty_Customer FOREIGN KEY ([CustomerKey]) REFERENCES [dbo].[DimCustomer]([CustomerKey]);
GO
ALTER TABLE [dbo].[FactLoyalty] ADD CONSTRAINT FK_FactLoyalty_Date FOREIGN KEY ([DateKey]) REFERENCES [dbo].[DimDate]([DateKey]);
GO
ALTER TABLE [dbo].[FactLoyalty] ADD CONSTRAINT FK_FactLoyalty_Shop FOREIGN KEY ([ShopKey]) REFERENCES [dbo].[DimShop]([ShopKey]);