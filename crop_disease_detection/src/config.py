from pathlib import Path

# ──────────────────────────────────────────────────────────────
# DSO Root  →  crop_disease_detection/
# Project Root  →  ProjetPi/
# ──────────────────────────────────────────────────────────────
DSO_ROOT     = Path(__file__).resolve().parent.parent   # crop_disease_detection/
PROJECT_ROOT = DSO_ROOT.parent                          # ProjetPi/

# ── Data (shared dataset lives in the project's Data/ folder) ─
DATA_DIR  = PROJECT_ROOT / "Data" / "Processed" / "Split_Dataset"
TRAIN_DIR = DATA_DIR / "train"
VAL_DIR   = DATA_DIR / "val"
TEST_DIR  = DATA_DIR / "test"

# ── DSO outputs ────────────────────────────────────────────────
MODELS_DIR          = DSO_ROOT / "models"
BENCHMARK_MODELS_DIR = MODELS_DIR / "benchmark"
REPORTS_DIR         = DSO_ROOT / "reports"
FIGURES_DIR         = REPORTS_DIR / "figures"
LOGS_DIR            = REPORTS_DIR / "logs"

# Ensure output directories exist
for _p in [MODELS_DIR, BENCHMARK_MODELS_DIR, REPORTS_DIR, FIGURES_DIR, LOGS_DIR]:
    _p.mkdir(parents=True, exist_ok=True)

# ── Model & Image settings ─────────────────────────────────────
IMG_SIZE     = (224, 224)
INPUT_SHAPE  = (224, 224, 3)
NUM_CLASSES  = 16          # 15 plant diseases + 1 background

# ── Training hyperparameters ───────────────────────────────────
BATCH_SIZE        = 32
# Calculate actual steps: (14674 images / 32 batch size) ≈ 458
STEPS_PER_EPOCH   = 458    # Updated after merging PlantDoc real-world images
PHASE_1_EPOCHS    = 10     # Transfer-learning head warmup (matches best model.py)
PHASE_2_EPOCHS    = 5      # Fine-tuning with frozen BatchNorm (matches best model.py)
PHASE_1_LR        = 1e-3
PHASE_2_LR        = 1e-5

# ── Architectures to benchmark ─────────────────────────────────
ARCHITECTURES = ["MobileNetV3Large", "EfficientNetB0", "ResNet50V2"]
