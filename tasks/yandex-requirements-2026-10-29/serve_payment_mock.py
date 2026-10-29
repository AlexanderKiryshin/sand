from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
import struct
import zlib

root = Path(__file__).resolve().parent / 'new-build-local-mock'
# Deterministic test fixture, a 32px yen symbol; production uses the SDK URL.
rows = ['10001','01010','00100','11111','00100','11111','00100']
raw = bytearray()
for y in range(32):
    raw.append(0)
    for x in range(32):
        ix, iy = (x-6)//4, (y-2)//4
        ink = 0 <= ix < 5 and 0 <= iy < 7 and rows[iy][ix] == '1'
        raw.extend((30,50,40,255) if ink else (255,255,255,0))
def chunk(kind, data):
    return struct.pack('>I',len(data)) + kind + data + struct.pack('>I',zlib.crc32(kind+data)&0xffffffff)
png = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR',struct.pack('>2I5B',32,32,8,6,0,0,0)) + chunk(b'IDAT',zlib.compress(raw)) + chunk(b'IEND',b'')
(root/'currency-mock.png').write_bytes(png)
class Handler(SimpleHTTPRequestHandler):
    def __init__(self,*args,**kwargs): super().__init__(*args,directory=str(root),**kwargs)
    def guess_type(self,path): return super().guess_type(path[:-3] if path.endswith('.br') else path)
    def end_headers(self):
        if self.path.split('?')[0].endswith('.br'): self.send_header('Content-Encoding','br')
        self.send_header('Content-Security-Policy',"connect-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval' blob:; img-src 'self' data: blob:")
        super().end_headers()
print('Local payment mock: http://127.0.0.1:8767',flush=True)
ThreadingHTTPServer(('127.0.0.1',8767),Handler).serve_forever()
