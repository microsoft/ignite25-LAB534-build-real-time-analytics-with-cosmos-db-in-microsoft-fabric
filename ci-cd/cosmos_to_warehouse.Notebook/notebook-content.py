# Fabric notebook source

# METADATA ********************

# META {
# META   "kernel_info": {
# META     "name": "synapse_pyspark"
# META   },
# META   "dependencies": {
# META     "lakehouse": {
# META       "default_lakehouse": "ca35456f-09c8-45d8-95cb-4054fa7f517f",
# META       "default_lakehouse_name": "fc_commerce_lh",
# META       "default_lakehouse_workspace_id": "dfd5246c-a34a-4402-b11a-f036c3ef7336",
# META       "known_lakehouses": [
# META         {
# META           "id": "ca35456f-09c8-45d8-95cb-4054fa7f517f"
# META         }
# META       ]
# META     }
# META   }
# META }

# CELL ********************


# Ensure that the schema ("control") exists before trying to write the table

spark.sql("CREATE SCHEMA IF NOT EXISTS control")

# Now, proceed with DataFrame creation and saving, as "control" schema should exist.

from pyspark.sql.types import StructType, StructField, StringType, TimestampType

schema = StructType([
    StructField("EntityName", StringType(), False),
    StructField("LastSuccessfulLoad", TimestampType(), False),
    StructField("LastAttempt", TimestampType(), True),
    StructField("LastStatus", StringType(), True),
    StructField("LastMessage", StringType(), True),
])

df_pl_watermarks = spark.createDataFrame([], schema)

df_pl_watermarks.write.format("delta").mode("overwrite").saveAsTable("control.PipelineWatermarks")

# METADATA ********************

# META {
# META   "language": "python",
# META   "language_group": "synapse_pyspark"
# META }

# CELL ********************

import com.microsoft.spark.fabric
from com.microsoft.spark.fabric.Constants import Constants

#  read lakehouse tables
df_cust = spark.sql("SELECT * FROM fc_commerce_lh.dbo.customers")
df_menuitems = spark.sql("SELECT * FROM fc_commerce_lh.dbo.menuitems") 
df_recommendations = spark.sql("SELECT * FROM fc_commerce_lh.dbo.recommendations")
df_shops = spark.sql("SELECT * FROM fc_commerce_lh.dbo.shops")

# read warehoouse tables
df_dimCust = spark.read.synapsesql("fc_commerce_wh.dbo.DimCustomer")

# METADATA ********************

# META {
# META   "language": "python",
# META   "language_group": "synapse_pyspark"
# META }
