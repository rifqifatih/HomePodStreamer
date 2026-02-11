#!/bin/bash
set -e

MEDIA_DIR="/srv/media"
FIFO_PATH="$MEDIA_DIR/pcm_input"

# Create media directory and FIFO pipe
mkdir -p "$MEDIA_DIR"
rm -f "$FIFO_PATH"
mkfifo "$FIFO_PATH"

# Hold the FIFO read-end open so socat can connect without blocking.
# Uses O_RDONLY|O_NONBLOCK so it doesn't consume data or block.
python3 -c "
import os, signal, time
fd = os.open('$FIFO_PATH', os.O_RDONLY | os.O_NONBLOCK)
signal.signal(signal.SIGTERM, lambda *a: (os.close(fd), exit(0)))
while True:
    time.sleep(3600)
" &
FIFO_HOLDER_PID=$!
echo "FIFO holder started (PID: $FIFO_HOLDER_PID)"

# Start socat to bridge TCP port 5555 to FIFO pipe
socat TCP-LISTEN:5555,reuseaddr,fork OPEN:"$FIFO_PATH",wronly &
SOCAT_PID=$!
echo "socat started (PID: $SOCAT_PID)"

# Ensure D-Bus is running (systemd should handle this, but just in case)
if ! pgrep -x dbus-daemon > /dev/null 2>&1; then
    mkdir -p /run/dbus
    dbus-daemon --system --nofork &
    sleep 1
    echo "dbus started"
else
    echo "dbus already running"
fi

# Ensure Avahi is running
if ! pgrep -x avahi-daemon > /dev/null 2>&1; then
    avahi-daemon -D
    sleep 1
    echo "avahi started"
else
    echo "avahi already running"
fi

# Cleanup on exit
cleanup() {
    echo "Stopping owntone services..."
    kill $FIFO_HOLDER_PID 2>/dev/null || true
    kill $SOCAT_PID 2>/dev/null || true
    pkill -f "avahi-publish" 2>/dev/null || true
    rm -f "$FIFO_PATH"
    echo "Cleanup done"
}
trap cleanup EXIT INT TERM

# Start owntone in foreground (blocks until killed)
echo "Starting owntone..."
/usr/sbin/owntone -f
