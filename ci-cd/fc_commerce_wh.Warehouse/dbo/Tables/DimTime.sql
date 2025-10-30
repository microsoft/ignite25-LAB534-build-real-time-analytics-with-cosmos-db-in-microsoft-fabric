CREATE TABLE [dbo].[DimTime] (

	[TimeKey] int NOT NULL, 
	[FullTime] time(3) NOT NULL, 
	[Hour] int NULL, 
	[Hour12] int NULL, 
	[AMPM] varchar(2) NULL, 
	[TimeOfDay] varchar(20) NULL, 
	[BusinessPeriod] varchar(20) NULL
);


GO
ALTER TABLE [dbo].[DimTime] ADD CONSTRAINT PK_DimTime primary key NONCLUSTERED ([TimeKey]);
GO
ALTER TABLE [dbo].[DimTime] ADD CONSTRAINT UQ_DimTime_FullTime unique NONCLUSTERED ([FullTime]);