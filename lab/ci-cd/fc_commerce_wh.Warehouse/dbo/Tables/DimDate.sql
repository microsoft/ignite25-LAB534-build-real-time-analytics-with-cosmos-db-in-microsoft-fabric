CREATE TABLE [dbo].[DimDate] (

	[DateKey] int NOT NULL, 
	[FullDate] date NOT NULL, 
	[DayOfWeek] int NULL, 
	[DayName] varchar(10) NULL, 
	[MonthNumber] int NULL, 
	[MonthName] varchar(10) NULL, 
	[Quarter] int NULL, 
	[Year] int NULL, 
	[IsWeekend] bit NULL, 
	[IsHoliday] bit NULL
);


GO
ALTER TABLE [dbo].[DimDate] ADD CONSTRAINT PK_DimDate primary key NONCLUSTERED ([DateKey]);
GO
ALTER TABLE [dbo].[DimDate] ADD CONSTRAINT UQ_DimDate_FullDate unique NONCLUSTERED ([FullDate]);