#!/usr/bin/env bash
set -euo pipefail

sudo apt-get update
sudo apt-get install -y qemu-system-x86 ovmf swtpm socat uuid-runtime

sudo modprobe kvm
grep -qw vmx /proc/cpuinfo && sudo modprobe kvm_intel || true
grep -qw svm /proc/cpuinfo && sudo modprobe kvm_amd || true

sudo groupadd -f kvm
sudo usermod -aG kvm "$USER"
sudo chgrp kvm /dev/kvm
sudo chmod 660 /dev/kvm

mkdir -p "$HOME/chromeos-lab"
install -m 755 "$(dirname "$0")/run.sh" "$HOME/chromeos-lab/run.sh"

echo 'done, run "wsl --shutdown" in powershell, then open ChromeOSEmu.exe.'
