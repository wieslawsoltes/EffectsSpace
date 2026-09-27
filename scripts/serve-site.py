#!/usr/bin/env python3
"""Serve the real browser artifact under its GitHub Pages project prefix."""
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlsplit
import argparse

parser = argparse.ArgumentParser()
parser.add_argument('--directory', default='artifacts/site')
parser.add_argument('--port', type=int, default=4173)
args = parser.parse_args()
root = Path(args.directory).resolve()
class Handler(SimpleHTTPRequestHandler):
    extensions_map = {**SimpleHTTPRequestHandler.extensions_map, '.wasm': 'application/wasm', '.webmanifest': 'application/manifest+json', '.js': 'text/javascript'}
    def __init__(self, *a, **kw):
        super().__init__(*a, directory=str(root), **kw)
    def translate_path(self, path):
        if path.startswith('/EffectsSpace/'):
            path = path[len('/EffectsSpace'):]
        return super().translate_path(path)
    def end_headers(self):
        self.send_header('Cache-Control', 'no-cache')
        super().end_headers()
ThreadingHTTPServer(('127.0.0.1', args.port), Handler).serve_forever()
