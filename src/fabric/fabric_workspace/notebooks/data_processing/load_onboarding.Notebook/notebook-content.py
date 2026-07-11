# Fabric notebook source

# METADATA ********************

# META {
# META   "kernel_info": {
# META     "name": "synapse_pyspark"
# META   }
# META }

# MARKDOWN ********************

# # Onboarding Data Loading
# 
# This notebook loads all Commodities & Global Markets onboarding CSV files from the lakehouse Files/data/onboarding directory into Delta tables.
# 
# ## Tables to Load:
# 1. **RelationshipManager** - Relationship managers covering the Commodities and Global Markets desks
# 2. **OnboardingCase** - Client onboarding cases and their stage/status
# 3. **KYCAssessment** - KYC/AML screening results linked to an onboarding case
# 4. **TradingAccount** - Trading accounts opened once a case is ready to trade

# CELL ********************

# Setup and Configuration
from pyspark.sql.functions import col

# Schema Configuration
SCHEMA_NAME = "onboarding"
DATA_PATH = "Files/data/onboarding"

# Ensure schema exists
spark.sql(f"CREATE DATABASE IF NOT EXISTS {SCHEMA_NAME}")
print(f"✅Loading '{SCHEMA_NAME}' schema from: {DATA_PATH}")

# METADATA ********************

# META {
# META   "language": "python",
# META   "language_group": "synapse_pyspark"
# META }

# CELL ********************

# 1. Load RelationshipManager Table (all STRING columns — no casts needed)
relationship_manager_df = spark.read.csv(f"{DATA_PATH}/RelationshipManager_Samples.csv", header=True, inferSchema=False)
relationship_manager_df.write.mode("overwrite").saveAsTable(f"{SCHEMA_NAME}.RelationshipManager")
relationship_manager_count = relationship_manager_df.count()

# METADATA ********************

# META {
# META   "language": "python",
# META   "language_group": "synapse_pyspark"
# META }

# CELL ********************

# 2. Load OnboardingCase Table
onboarding_case_df = spark.read.csv(f"{DATA_PATH}/OnboardingCase_Samples.csv", header=True, inferSchema=False)
onboarding_case_df = onboarding_case_df \
    .withColumn("OpenedDate", col("OpenedDate").cast("date")) \
    .withColumn("TargetGoLiveDate", col("TargetGoLiveDate").cast("date")) \
    .withColumn("ReadyToTradeDate", col("ReadyToTradeDate").cast("date"))
onboarding_case_df.write.mode("overwrite").saveAsTable(f"{SCHEMA_NAME}.OnboardingCase")
onboarding_case_count = onboarding_case_df.count()

# METADATA ********************

# META {
# META   "language": "python",
# META   "language_group": "synapse_pyspark"
# META }

# CELL ********************

# 3. Load KYCAssessment Table
kyc_assessment_df = spark.read.csv(f"{DATA_PATH}/KYCAssessment_Samples.csv", header=True, inferSchema=False)
kyc_assessment_df = kyc_assessment_df \
    .withColumn("ReviewedDate", col("ReviewedDate").cast("date"))
kyc_assessment_df.write.mode("overwrite").saveAsTable(f"{SCHEMA_NAME}.KYCAssessment")
kyc_assessment_count = kyc_assessment_df.count()

# METADATA ********************

# META {
# META   "language": "python",
# META   "language_group": "synapse_pyspark"
# META }

# CELL ********************

# 4. Load TradingAccount Table
trading_account_df = spark.read.csv(f"{DATA_PATH}/TradingAccount_Samples.csv", header=True, inferSchema=False)
trading_account_df = trading_account_df \
    .withColumn("OpenedDate", col("OpenedDate").cast("date"))
trading_account_df.write.mode("overwrite").saveAsTable(f"{SCHEMA_NAME}.TradingAccount")
trading_account_count = trading_account_df.count()

# METADATA ********************

# META {
# META   "language": "python",
# META   "language_group": "synapse_pyspark"
# META }

# CELL ********************

# Summary
total_records = relationship_manager_count + onboarding_case_count + kyc_assessment_count + trading_account_count
print(f"📊Onboarding schema: 4 tables, {total_records:,} records loaded")
print(f"   - RelationshipManager: {relationship_manager_count:,} records")
print(f"   - OnboardingCase: {onboarding_case_count:,} records")
print(f"   - KYCAssessment: {kyc_assessment_count:,} records")
print(f"   - TradingAccount: {trading_account_count:,} records")

# METADATA ********************

# META {
# META   "language": "python",
# META   "language_group": "synapse_pyspark"
# META }
