CREATE TABLE [dbo].[DimMenuItem] (

	[MenuItemKey] int NOT NULL, 
	[MenuItemId] varchar(64) NOT NULL, 
	[MenuItemName] varchar(128) NULL, 
	[Category] varchar(64) NULL, 
	[Price] decimal(10,2) NULL, 
	[IsRecommended] bit NULL, 
	[Calories] int NULL, 
	[IsActive] bit NULL, 
	[CreatedAt] datetime2(3) NULL, 
	[UpdatedAt] datetime2(3) NULL
);


GO
ALTER TABLE [dbo].[DimMenuItem] ADD CONSTRAINT PK_DimMenuItem primary key NONCLUSTERED ([MenuItemKey]);
GO
ALTER TABLE [dbo].[DimMenuItem] ADD CONSTRAINT UQ_DimMenuItem_MenuItemId unique NONCLUSTERED ([MenuItemId]);