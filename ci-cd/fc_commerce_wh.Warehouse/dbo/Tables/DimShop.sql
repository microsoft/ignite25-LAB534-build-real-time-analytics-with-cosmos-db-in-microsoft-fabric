CREATE TABLE [dbo].[DimShop] (

	[ShopKey] int NOT NULL, 
	[ShopId] varchar(64) NOT NULL, 
	[ShopName] varchar(128) NULL, 
	[AirportId] varchar(32) NULL, 
	[AirportName] varchar(128) NULL, 
	[Terminal] varchar(64) NULL, 
	[Timezone] varchar(64) NULL, 
	[IsActive] bit NULL, 
	[CreatedAt] datetime2(3) NULL, 
	[UpdatedAt] datetime2(3) NULL
);


GO
ALTER TABLE [dbo].[DimShop] ADD CONSTRAINT PK_DimShop primary key NONCLUSTERED ([ShopKey]);
GO
ALTER TABLE [dbo].[DimShop] ADD CONSTRAINT UQ_DimShop_ShopId unique NONCLUSTERED ([ShopId]);