from pathlib import Path

# Project Root
ROOT_DIR = Path(__file__).resolve().parent.parent

# Data Paths
DATA_DIR = ROOT_DIR / "Data" / "Processed" / "Split_Dataset"
TRAIN_DIR = DATA_DIR / "train"
VAL_DIR = DATA_DIR / "val"
TEST_DIR = DATA_DIR / "test"

# Output Paths
MODELS_DIR = ROOT_DIR / "Models"
REPORTS_DIR = ROOT_DIR / "Reports"
LOGS_DIR = REPORTS_DIR / "logs"

# Ensure directories exist
for path in [MODELS_DIR, REPORTS_DIR, LOGS_DIR]:
    path.mkdir(parents=True, exist_ok=True)

# Shared Hyperparameters
BATCH_SIZE = 32
IMG_SIZE = (224, 224)
INPUT_SHAPE = (224, 224, 3)
NUM_CLASSES = 16

# Training Hyperparameters
PHASE_1_EPOCHS = 5
PHASE_2_EPOCHS = 3
PHASE_1_LR = 1e-3
PHASE_2_LR = 1e-5
STEPS_PER_EPOCH = 500 # Optimized for benchmark
