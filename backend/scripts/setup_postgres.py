import psycopg2
from psycopg2.extensions import ISOLATION_LEVEL_AUTOCOMMIT

def setup_db():
    try:
        # Connect to default postgres DB
        conn = psycopg2.connect(
            user='postgres',
            host='localhost',
            password='sania_pass',
            port='5432',
            dbname='postgres'
        )
        conn.set_isolation_level(ISOLATION_LEVEL_AUTOCOMMIT)
        cur = conn.cursor()
        
        # Check if sania_db exists
        cur.execute("SELECT 1 FROM pg_database WHERE datname = 'sania_db'")
        exists = cur.fetchone()
        
        if not exists:
            cur.execute("CREATE DATABASE sania_db")
            print("Database 'sania_db' created successfully.")
        else:
            print("Database 'sania_db' already exists.")
            
        cur.close()
        conn.close()
    except Exception as e:
        print(f"Error setting up database: {e}")

if __name__ == "__main__":
    setup_db()
