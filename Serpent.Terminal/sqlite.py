import sqlite3
import random
import string
import time
import sys

def log(msg):
    print(f"[{time.strftime('%H:%M:%S')}] {msg}")

path = "app.db"
log(f"open db: {path}")
conn = sqlite3.connect(path)
cursor = conn.cursor()

# ---- Integrity check ----
log("integrity_check")
cursor.execute("PRAGMA integrity_check")
log(f"integrity result: {cursor.fetchone()}")

# ---- Reset ----
log("drop table if exists")
cursor.execute("DROP TABLE IF EXISTS stocks")

# ---- Create table ----
log("create table")
cursor.execute("""
CREATE TABLE stocks (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    date TEXT,
    trans TEXT,
    symbol TEXT,
    qty REAL,
    price REAL
)
""")

# ---- Indexes ----
log("create indexes")
cursor.execute("CREATE INDEX idx_symbol ON stocks(symbol)")
cursor.execute("CREATE INDEX idx_date ON stocks(date)")

# ---- Single write ----
log("single insert")
cursor.execute(
    "INSERT INTO stocks (date, trans, symbol, qty, price) VALUES (?, ?, ?, ?, ?)",
    ("2006-01-05", "BUY", "RHAT", 100, 35.14)
)
conn.commit()
log("single insert committed")

# ---- Single read ----
log("single read")
cursor.execute("SELECT * FROM stocks WHERE symbol = ?", ("RHAT",))
log(f"single read result: {cursor.fetchone()}")

# ---- Bulk write ----
def rand_symbol():
    return ''.join(random.choices(string.ascii_uppercase, k=4))

log("prepare bulk rows")
rows = [
    (
        f"2024-01-{(i % 28) + 1:02d}",
        random.choice(["BUY", "SELL"]),
        rand_symbol(),
        random.randint(1, 1000),
        round(random.uniform(10, 500), 2)
    )
    for i in range(10_000)
]

log("bulk insert start")
t0 = time.time()
cursor.executemany(
    "INSERT INTO stocks (date, trans, symbol, qty, price) VALUES (?, ?, ?, ?, ?)",
    rows
)
conn.commit()
log(f"bulk insert committed ({time.time() - t0:.3f}s)")

# ---- Bulk read ----
log("bulk read start")
t0 = time.time()
cursor.execute("SELECT * FROM stocks")
all_rows = cursor.fetchall()
log(f"bulk read done rows={len(all_rows)} ({time.time() - t0:.3f}s)")

# ---- Indexed read ----
log("indexed read start")
t0 = time.time()
cursor.execute("SELECT * FROM stocks WHERE symbol = ?", (rows[5000][2],))
filtered = cursor.fetchall()
log(f"indexed read done rows={len(filtered)} ({time.time() - t0:.3f}s)")

# ---- Aggregation ----
log("group by start")
cursor.execute("SELECT symbol, COUNT(*) FROM stocks GROUP BY symbol")
sample = cursor.fetchmany(5)
log(f"group by sample: {sample}")

# ---- Close ----
log("close connection")
conn.close()

log("sqlite ok")