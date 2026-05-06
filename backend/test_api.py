import requests

BASE = "http://localhost:8000/api/v1"

r = requests.post(f"{BASE}/auth/login", data={"username": "farmer@agrismart.tn", "password": "Farmer123!"})
token = r.json()["access_token"]
h = {"Authorization": f"Bearer {token}"}

fields = requests.get(f"{BASE}/fields/", headers=h).json()
fid = fields[0]["id"]

for name, url in [
    ("Fields", f"{BASE}/fields/"),
    ("Sensors", f"{BASE}/sensors/{fid}?days=7"),
    ("NDVI", f"{BASE}/ndvi/{fid}?weeks=12"),
    ("Irrigation", f"{BASE}/fields/{fid}/irrigation-logs"),
    ("Alerts", f"{BASE}/alerts/"),
    ("Scans", f"{BASE}/scans/"),
    ("Animals", f"{BASE}/animals/"),
]:
    r = requests.get(url, headers=h)
    if r.status_code == 200:
        data = r.json()
        print(f"OK  {name}: {len(data)} records")
    else:
        print(f"ERR {name}: {r.status_code} -> {r.text[:300]}")
