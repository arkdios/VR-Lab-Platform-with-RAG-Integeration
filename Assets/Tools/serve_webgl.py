"""
Small test server for a Unity Web (WebGL + WebXR) build. Run it from PowerShell:

    python tools\\serve_webgl.py Builds\\WebGL 8080

Then open http://localhost:8080 in the browser. Stop it with Ctrl+C.

Why not "python -m http.server"? When Unity compression is on, the build files end in .br or .gz.
The browser can open them only if the server says how they are packed (the Content-Encoding header),
and "python -m http.server" does not say it. This script does. It also sends the right type for .wasm.
It listens on this computer only (127.0.0.1). The address "localhost" counts as a secure page,
so WebXR (the VR button) is allowed on it.
"""
import http.server
import os
import sys
from urllib.parse import urlsplit

# The file type the browser must see, chosen by the name without .br or .gz.
TYPES = {
    ".wasm": "application/wasm",
    ".js": "text/javascript",
    ".data": "application/octet-stream",
    ".json": "application/json",
    ".html": "text/html",
    ".css": "text/css",
    ".png": "image/png",
    ".ico": "image/x-icon",
}
PACKING = {".br": "br", ".gz": "gzip"}   # file ending -> Content-Encoding value


class WebGLHandler(http.server.SimpleHTTPRequestHandler):
    def packing_of(self):
        """The Content-Encoding value for the requested file, or None for a plain file."""
        path = urlsplit(self.path).path
        return PACKING.get(os.path.splitext(path)[1])

    def guess_type(self, path):
        unpacked = os.path.splitext(path)[0] if os.path.splitext(path)[1] in PACKING else path
        return TYPES.get(os.path.splitext(unpacked)[1], "application/octet-stream")

    def end_headers(self):
        packing = self.packing_of()
        if packing:
            self.send_header("Content-Encoding", packing)
        self.send_header("Cache-Control", "no-store")   # a new build always loads, never an old copy
        super().end_headers()


def main():
    folder = sys.argv[1] if len(sys.argv) > 1 else "."
    port = int(sys.argv[2]) if len(sys.argv) > 2 else 8080
    if not os.path.isfile(os.path.join(folder, "index.html")):
        sys.exit(f"No index.html in '{folder}'. Give the build folder, for example: Builds\\WebGL")

    handler = lambda *args, **kwargs: WebGLHandler(*args, directory=folder, **kwargs)
    with http.server.ThreadingHTTPServer(("127.0.0.1", port), handler) as server:
        print(f"Serving '{folder}' at http://localhost:{port}  (Ctrl+C to stop)")
        try:
            server.serve_forever()
        except KeyboardInterrupt:
            print("\nStopped.")


if __name__ == "__main__":
    main()
