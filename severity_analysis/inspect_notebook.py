import json

notebook_path = 'Severity_Model_Training.ipynb'
try:
    with open(notebook_path, 'r', encoding='utf-8') as f:
        nb = json.load(f)
    print(f"Notebook loaded with {len(nb.get('cells', []))} cells.")
    
    # Let's just find the last few code cells and print their outputs exactly
    for i in range(len(nb.get('cells', [])) - 1, -1, -1):
        cell = nb['cells'][i]
        if cell.get('cell_type') == 'code':
            source = "".join(cell.get('source', []))
            outputs = cell.get('outputs', [])
            if outputs:
                print(f"\n--- Cell {i} ---")
                print(f"Source: {source[:100]}...")
                for out in outputs:
                    if 'text' in out:
                        print("".join(out['text']))
                    elif 'data' in out and 'text/plain' in out['data']:
                        print("".join(out['data']['text/plain']))
        # Limit to last 5 code cells with output
        if i < len(nb.get('cells', [])) - 10:
             break
                    
except Exception as e:
    print(f"Error: {e}")
