CREATE TABLE [dbo].[DimCustomer] (

	[CustomerKey] int NOT NULL, 
	[CustomerId] varchar(64) NOT NULL, 
	[CustomerName] varchar(128) NULL, 
	[Email] varchar(256) NULL, 
	[PreferredAirport] varchar(32) NULL, 
	[FavoriteDrink] varchar(128) NULL, 
	[LoyaltyTier] varchar(32) NULL, 
	[RegistrationDate] date NULL, 
	[IsActive] bit NULL, 
	[CreatedAt] datetime2(3) NULL, 
	[UpdatedAt] datetime2(3) NULL
);


GO
ALTER TABLE [dbo].[DimCustomer] ADD CONSTRAINT PK_DimCustomer primary key NONCLUSTERED ([CustomerKey]);
GO
ALTER TABLE [dbo].[DimCustomer] ADD CONSTRAINT UQ_DimCustomer_CustomerId unique NONCLUSTERED ([CustomerId]);