import sys
import os
import json

# Setup paths to import app
sys.path.append(os.path.join(os.getcwd()))

from app.services.ndvi_diagnostic import ndvi_diagnostic_service

# Mock polygon (a small square in a farm area)
mock_polygon = json.dumps([
    [36.8, 10.1],
    [36.81, 10.1],
    [36.81, 10.11],
    [36.8, 10.11]
])

print("Testing NDVI Diagnostic Service...")
result = ndvi_diagnostic_service.generate_mock_heatmap(mock_polygon)

print("\n--- RESULT ---")
print(json.dumps(result, indent=2))
