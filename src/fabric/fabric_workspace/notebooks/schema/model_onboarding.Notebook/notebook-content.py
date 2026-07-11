# Fabric notebook source

# METADATA ********************

# META {
# META   "kernel_info": {
# META     "name": "synapse_pyspark"
# META   }
# META }

# MARKDOWN ********************

# # Data Model for Commodities & Global Markets Onboarding
# 
# ## Schema Structure
# - **Onboarding & Relationship Management (4 tables)**:
# - RelationshipManager, Samples Ready: RelationshipManager_Samples.csv
# - OnboardingCase, Samples Ready: OnboardingCase_Samples.csv
# - KYCAssessment, Samples Ready: KYCAssessment_Samples.csv
# - TradingAccount, Samples Ready: TradingAccount_Samples.csv

# CELL ********************

################################################################################################
# Schema Configuration - You can define different value here
################################################################################################

# Schema Configuration
SCHEMA_NAME = "onboarding"
spark.sql(f"CREATE DATABASE IF NOT EXISTS {SCHEMA_NAME}")
print(f"✅ {SCHEMA_NAME} schema ready!")

# METADATA ********************

# META {
# META   "language": "python",
# META   "language_group": "synapse_pyspark"
# META }

# CELL ********************

################################################################################################
# Onboarding Domain - Relationship Managers, Onboarding Cases, KYC and Trading Accounts. 4 Tables
################################################################################################

# 1. Create RelationshipManager table
TABLE_NAME = "RelationshipManager"
create_table_sql = f"""
CREATE TABLE IF NOT EXISTS {SCHEMA_NAME}.{TABLE_NAME} (
    RelationshipManagerID STRING,
    FirstName STRING,
    LastName STRING,
    Desk STRING,          -- Commodities, Global Markets
    Region STRING,
    Email STRING
)
USING DELTA
"""
spark.sql(create_table_sql)
print(f"✅ {SCHEMA_NAME}.{TABLE_NAME} table created!")

# 2. Create OnboardingCase table
TABLE_NAME = "OnboardingCase"
create_table_sql = f"""
CREATE TABLE IF NOT EXISTS {SCHEMA_NAME}.{TABLE_NAME} (
    CaseID STRING,
    CustomerID STRING,
    RelationshipManagerID STRING,
    BusinessLine STRING,  -- Commodities, Global Markets
    CaseStatus STRING,    -- Open, InReview, Approved, ReadyToTrade
    Stage STRING,         -- Intake, Screening, DocumentCollection, Decision, Setup
    OpenedDate DATE,
    TargetGoLiveDate DATE,
    ReadyToTradeDate DATE
)
USING DELTA
"""
spark.sql(create_table_sql)
print(f"✅ {SCHEMA_NAME}.{TABLE_NAME} table created!")

# 3. Create KYCAssessment table
TABLE_NAME = "KYCAssessment"
create_table_sql = f"""
CREATE TABLE IF NOT EXISTS {SCHEMA_NAME}.{TABLE_NAME} (
    KYCID STRING,
    CaseID STRING,
    RiskRating STRING,               -- Low, Medium, High
    SanctionsScreeningResult STRING, -- Clear, Hit, PendingReview
    PepScreeningResult STRING,       -- Clear, Hit, PendingReview
    AdverseMediaResult STRING,       -- Clear, Hit, PendingReview
    ApprovalStatus STRING,           -- Pending, Approved, Rejected
    ReviewedBy STRING,
    ReviewedDate DATE
)
USING DELTA
"""
spark.sql(create_table_sql)
print(f"✅ {SCHEMA_NAME}.{TABLE_NAME} table created!")

# 4. Create TradingAccount table
TABLE_NAME = "TradingAccount"
create_table_sql = f"""
CREATE TABLE IF NOT EXISTS {SCHEMA_NAME}.{TABLE_NAME} (
    TradingAccountID STRING,
    CustomerID STRING,
    CaseID STRING,
    ProductLine STRING,   -- Commodities, FX, Rates, Equities
    AccountStatus STRING, -- Active, Suspended, Closed
    OpenedDate DATE,
    IsoCurrencyCode STRING
)
USING DELTA
"""
spark.sql(create_table_sql)
print(f"✅ {SCHEMA_NAME}.{TABLE_NAME} table created!")

# CELL ********************

print(f"🎉 {SCHEMA_NAME.upper()} DOMAIN COMPLETE!")

# METADATA ********************

# META {
# META   "language": "python",
# META   "language_group": "synapse_pyspark"
# META }
