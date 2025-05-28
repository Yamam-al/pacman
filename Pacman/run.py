import subprocess

# Pfad zur ausführbaren Datei
exe_path = r"C:\Users\alsho\RiderProjects\pacman2\Pacman\bin\Debug\net8.0\Pacman.exe"

# Anzahl der Durchläufe
num_runs = 100

for i in range(num_runs):
    print(f"Run {i+1}/{num_runs}")
    result = subprocess.Popen(exe_path)
    result.wait()
    # Ausgabe anzeigen oder loggen
    print("Exit code:", result.returncode)
    if result.stdout:
        print("Output:", result.stdout.strip())
    if result.stderr:
        print("Error:", result.stderr.strip())
