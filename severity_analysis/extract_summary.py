import json

notebook_path = 'Severity_Model_Training.ipynb'
try:
    with open(notebook_path, 'r', encoding='utf-8') as f:
        nb = json.load(f)
    print("Notebook loaded.")
    for block_num, cell in enumerate(nb.get('cells', [])):
        if cell.get('cell_type') == 'code':
            outputs = cell.get('outputs', [])
            for out in outputs:
                if out.get('output_type') == 'stream':
                    text = "".join(out.get('text', []))
                    if 'Epoch' in text or 'loss' in text or 'accuracy' in text or 'iou' in text or 'dice' in text or 'Test' in text or 'Validation' in text:
                        print(f"--- Cell {block_num} Output ---")
                        lines = text.split('\n')
                        if len(lines) > 20: 
                            print('\n'.join(lines[:10]))
                            print('...')
                            print('\n'.join(lines[-10:]))
                        else:
                            print(text)
                elif out.get('output_type') == 'execute_result' or out.get('output_type') == 'display_data':
                    data = out.get('data', {})
                    if 'text/plain' in data:
                        text = "".join(data['text/plain'])
                        if 'loss' in text or 'accuracy' in text or 'iou' in text:
                            print(f"--- Cell {block_num} Output (Result) ---")
                            print(text)
except Exception as e:
    print(e)
