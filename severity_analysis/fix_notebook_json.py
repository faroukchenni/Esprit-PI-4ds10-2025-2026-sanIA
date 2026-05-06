"""Repair TDSP_Severity_Model.ipynb: truncated base64 in last cell + missing notebook footer."""
from __future__ import annotations

import json
import shutil
from pathlib import Path

NOTEBOOK = Path(__file__).resolve().parent / "TDSP_Severity_Model.ipynb"


def main() -> None:
    text = NOTEBOOK.read_text(encoding="utf-8")
    marker = '"image/png": "'
    idx = text.rfind(marker)
    if idx < 0:
        raise SystemExit("Could not find image/png output to repair.")

    prefix = text[:idx] + '"image/png": ""'

    source_lines = [
        "# --- Test set evaluation (cell restored after .ipynb JSON was truncated mid-base64) ---\n",
        "print('\\n--- Test set (held-out originals) ---')\n",
        "_ = model.evaluate(X_test, Y_test, batch_size=BATCH_SIZE, verbose=1)\n",
    ]
    src_parts = ",\n".join("    " + json.dumps(s) for s in source_lines)

    suffix = f""",
     "text/plain": [
      "<matplotlib.figure.Figure; test eval figure removed during repair>"
     ]
    }},
    "metadata": {{}},
    "output_type": "display_data"
   }}
  ],
  "source": [
{src_parts}
  ]
 }}
 ],
 "metadata": {{
  "kernelspec": {{
   "display_name": "Python 3",
   "language": "python",
   "name": "python3"
  }},
  "language_info": {{
   "name": "python",
   "pygments_lexer": "ipython3"
  }}
 }},
 "nbformat": 4,
 "nbformat_minor": 5
}}
"""

    repaired = prefix + suffix
    json.loads(repaired)

    backup = NOTEBOOK.with_suffix(".ipynb.broken-bak")
    shutil.copy2(NOTEBOOK, backup)
    NOTEBOOK.write_text(repaired, encoding="utf-8")
    print(f"Repaired notebook written. Backup: {backup}")


if __name__ == "__main__":
    main()
