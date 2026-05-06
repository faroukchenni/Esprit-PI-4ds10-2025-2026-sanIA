import json
import re

notebook_path = 'Severity_Model_Training.ipynb'
try:
    with open(notebook_path, 'r', encoding='utf-8') as f:
        nb = json.load(f)
    print("Notebook loaded.")
    for block_num, cell in enumerate(nb.get('cells', [])):
        if cell.get('cell_type') == 'code':
            source = "".join(cell.get('source', []))
            outputs = cell.get('outputs', [])
            
            output_texts = []
            for out in outputs:
                if 'text' in out:
                    output_texts.append("".join(out['text']))
                elif 'data' in out and 'text/plain' in out['data']:
                    output_texts.append("".join(out['data']['text/plain']))
            
            full_output = "\n".join(output_texts)
            
            if 'model.evaluate' in source or 'val_accuracy' in full_output or 'val_loss' in full_output or 'dice' in full_output or 'Precision' in full_output:
                lines = full_output.split('\n')
                # clean up carriage returns common in TF progress bars
                clean_lines = [line.split('\r')[-1] for line in lines if line.strip()]
                print(f"--- Cell {block_num} Matches ---")
                print('Source snippet: ' + source[:100] + '...')
                if len(clean_lines) > 30:
                    print('\n'.join(clean_lines[:15]))
                    print('...')
                    print('\n'.join(clean_lines[-15:]))
                else:
                    print('\n'.join(clean_lines))
                    
except Exception as e:
    print(e)
