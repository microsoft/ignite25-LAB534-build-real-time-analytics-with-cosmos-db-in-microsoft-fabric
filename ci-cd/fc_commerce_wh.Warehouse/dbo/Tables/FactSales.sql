CREATE TABLE [dbo].[FactSales] (

	[SalesKey] bigint NOT NULL, 
	[TransactionId] varchar(64) NOT NULL, 
	[DateKey] int NOT NULL, 
	[TimeKey] int NOT NULL, 
	[CustomerKey] int NOT NULL, 
	[ShopKey] int NOT NULL, 
	[MenuItemKey] int NOT NULL, 
	[Quantity] int NOT NULL, 
	[UnitPrice] decimal(10,2) NOT NULL, 
	[TotalAmount] decimal(10,2) NOT NULL, 
	[LoyaltyPointsEarned] int NULL, 
	[LoyaltyPointsRedeemed] int NULL, 
	[PaymentMethod] varchar(32) NULL, 
	[Size] varchar(32) NULL, 
	[SourceSystem] varchar(50) NULL, 
	[CreatedAt] datetime2(3) NULL
);


GO
ALTER TABLE [dbo].[FactSales] ADD CONSTRAINT PK_FactSales primary key NONCLUSTERED ([SalesKey]);
GO
ALTER TABLE [dbo].[FactSales] ADD CONSTRAINT UQ_FactSales_TransactionId unique NONCLUSTERED ([TransactionId]);
GO
ALTER TABLE [dbo].[FactSales] ADD CONSTRAINT FK_FactSales_Customer FOREIGN KEY ([CustomerKey]) REFERENCES [dbo].[DimCustomer]([CustomerKey]);
GO
ALTER TABLE [dbo].[FactSales] ADD CONSTRAINT FK_FactSales_Date FOREIGN KEY ([DateKey]) REFERENCES [dbo].[DimDate]([DateKey]);
GO
ALTER TABLE [dbo].[FactSales] ADD CONSTRAINT FK_FactSales_MenuItem FOREIGN KEY ([MenuItemKey]) REFERENCES [dbo].[DimMenuItem]([MenuItemKey]);
GO
ALTER TABLE [dbo].[FactSales] ADD CONSTRAINT FK_FactSales_Shop FOREIGN KEY ([ShopKey]) REFERENCES [dbo].[DimShop]([ShopKey]);
GO
ALTER TABLE [dbo].[FactSales] ADD CONSTRAINT FK_FactSales_Time FOREIGN KEY ([TimeKey]) REFERENCES [dbo].[DimTime]([TimeKey]);