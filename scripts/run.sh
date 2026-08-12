#!/usr/bin/env bash
set -euo pipefail

lab="$HOME/chromeos-lab"
image="${IMAGE:-/mnt/c/ChromeOSLab/flex/chromeos-flex-compressed.qcow2}"
memory="${MEMORY:-4G}"
cores="${CORES:-6}"

mkdir -p "$lab"
exec 9>"$lab/run.lock"
flock -w 15 9 || { echo 'Another ChromeOS session is still stopping.' >&2; exit 1; }

[[ -r /dev/kvm && -w /dev/kvm ]] || { echo '/dev/kvm is not available.' >&2; exit 1; }
[[ -f "$image" ]] || { echo "Disk not found: $image" >&2; exit 1; }

mkdir -p "$lab/tpm2"
chmod 700 "$lab/tpm2"

[[ -f "$lab/OVMF_VARS_4M.fd" ]] ||
  install -m 600 /usr/share/OVMF/OVMF_VARS_4M.fd "$lab/OVMF_VARS_4M.fd"

if [[ ! -f "$lab/vm.uuid" ]]; then
  uuidgen > "$lab/vm.uuid"
  chmod 600 "$lab/vm.uuid"
fi

rm -f "$lab/tpm2/swtpm.sock" "$lab/qemu-monitor.sock" "$lab/serial.sock"

swtpm socket \
  --tpm2 \
  --tpmstate "dir=$lab/tpm2" \
  --ctrl "type=unixio,path=$lab/tpm2/swtpm.sock" \
  --log "file=$lab/tpm2/swtpm.log,level=1" &
tpm_pid=$!

cleanup() {
  kill "$tpm_pid" 2>/dev/null || true
  rm -f "$lab/tpm2/swtpm.sock" "$lab/qemu-monitor.sock" "$lab/serial.sock"
}
trap cleanup EXIT INT TERM

for _ in {1..100}; do
  [[ -S "$lab/tpm2/swtpm.sock" ]] && kill -0 "$tpm_pid" 2>/dev/null && break
  sleep 0.05
done
[[ -S "$lab/tpm2/swtpm.sock" ]] && kill -0 "$tpm_pid" 2>/dev/null || {
  echo 'TPM failed to start.' >&2
  exit 1
}

qemu-system-x86_64 \
  -name ChromeOS-Flex \
  -machine q35,accel=kvm \
  -cpu host \
  -smp "$cores",sockets=1,cores="$cores",threads=1 \
  -m "$memory" \
  -uuid "$(<"$lab/vm.uuid")" \
  -rtc base=utc,clock=host \
  -display none \
  -vnc 127.0.0.1:1,lossy=off,non-adaptive=on \
  -device virtio-vga,xres=1280,yres=800,edid=on \
  -drive if=pflash,format=raw,readonly=on,file=/usr/share/OVMF/OVMF_CODE_4M.fd \
  -drive if=pflash,format=raw,file="$lab/OVMF_VARS_4M.fd" \
  -device qemu-xhci,id=xhci \
  -drive if=none,id=os,file="$image",format=qcow2,cache=writeback,aio=threads \
  -device usb-storage,bus=xhci.0,drive=os,bootindex=1,removable=on \
  -netdev user,id=net \
  -device virtio-net-pci,netdev=net,mac=52:54:00:43:52:01 \
  -chardev socket,id=tpm,path="$lab/tpm2/swtpm.sock" \
  -tpmdev emulator,id=tpm0,chardev=tpm \
  -device tpm-crb,tpmdev=tpm0 \
  -device virtio-rng-pci \
  -device usb-kbd,bus=xhci.0 \
  -device usb-tablet,bus=xhci.0 \
  -audiodev none,id=noaudio \
  -chardev "socket,id=serial,path=$lab/serial.sock,server=on,wait=off,logfile=$lab/serial.log" \
  -serial chardev:serial \
  -monitor "unix:$lab/qemu-monitor.sock,server=on,wait=off" \
  2>&1 | tee "$lab/qemu.log"
