#!/bin/bash
set -e

echo "=== Installing runtime dependencies ==="
sudo apt-get update -qq
sudo apt-get install -y -qq socat avahi-daemon avahi-utils dbus

echo "=== Installing build dependencies ==="
sudo apt-get install -y -qq \
  build-essential git autotools-dev autoconf automake libtool gettext gawk \
  gperf bison flex libconfuse-dev libunistring-dev libsqlite3-dev \
  libavcodec-dev libavformat-dev libavfilter-dev libswscale-dev libavutil-dev \
  libxml2-dev libgcrypt20-dev libavahi-client-dev zlib1g-dev \
  libevent-dev libplist-dev libsodium-dev libjson-c-dev libwebsockets-dev \
  libcurl4-openssl-dev libprotobuf-c-dev

echo "=== Cloning owntone-server ==="
cd /tmp
rm -rf owntone-server
git clone --depth 1 https://github.com/owntone/owntone-server.git
cd owntone-server

echo "=== Building owntone ==="
autoreconf -i
./configure --prefix=/usr --sysconfdir=/etc --localstatedir=/var --enable-install-user
make -j$(nproc)

echo "=== Installing owntone ==="
sudo make install

echo "=== Setting up directories ==="
sudo mkdir -p /srv/media
sudo mkdir -p /var/cache/owntone
sudo mkdir -p /var/log

echo "=== Done! owntone installed ==="
owntone --version 2>&1 || echo "owntone installed at $(which owntone)"
