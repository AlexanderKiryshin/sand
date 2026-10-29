from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

bundle = Path(__file__).resolve().parent / 'build18'

class Handler(SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=str(bundle), **kwargs)

    def guess_type(self, path):
        return super().guess_type(path[:-3] if path.endswith('.br') else path)

    def end_headers(self):
        if self.path.split('?')[0].endswith('.br'):
            self.send_header('Content-Encoding', 'br')
        super().end_headers()

print('Local archive audit: http://127.0.0.1:8766', flush=True)
ThreadingHTTPServer(('127.0.0.1', 8766), Handler).serve_forever()
