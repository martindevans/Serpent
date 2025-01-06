import sqlite3
import os
import time

time.sleep(1)
os.mkdir("foo");

# Connect to an SQLite database (or create it if it doesn't exist)
conn = sqlite3.connect(':memory:')

# Create a cursor object using the cursor() method
cursor = conn.cursor()

# Check db
cursor.execute('pragma integrity_check')

# Create table
cursor.execute('CREATE TABLE IF NOT EXISTS stocks (date text, trans text, symbol text, qty real, price real)')

# Insert a row of data
cursor.execute("INSERT INTO stocks VALUES ('2006-01-05','BUY','RHAT',100,35.14)")

# Save (commit) the changes
conn.commit()

# Close the connection
conn.close()

print("sqlite ok")