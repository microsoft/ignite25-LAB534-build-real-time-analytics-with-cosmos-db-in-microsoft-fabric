# Real-Time Streaming and Visualization of POS Events

In this first part of the lab, you set up the Microsoft Fabric environment needed to complete the exercises in this lab.

This includes creating a new *Fabric workspace*, automated creation of a *Fabric Warehouse*, and loading data into the Data Warehouse that will be used in later exercises.

## Connect your Eventstream output to a KQL Database

1. In Fabric, open your Eventstream.

1. Click + Destination → KQL Database.

1. Create a KQL Database called +++*FourthCoffeeStreamDB*+++.

1. Name the table +++*TransactionsLive*+++.

1. Open a new query window and write the below to verify live ingestion:

+++TransactionsLive 
| take 10+++

