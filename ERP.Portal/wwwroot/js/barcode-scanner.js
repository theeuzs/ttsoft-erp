// ── barcode-scanner.js ──────────────────────────────────────────────────
// Leitura de código de barras pela câmera, usando ZXing (wwwroot/lib/zxing.min.js).
// Precisa ser carregado DEPOIS do zxing.min.js.

let _leitor = null;

window.iniciarScannerCodigoBarras = async function (videoElementId, dotNetHelper) {
    const video = document.getElementById(videoElementId);
    if (!video) {
        dotNetHelper.invokeMethodAsync('OnErroScanner', 'Elemento de vídeo não encontrado.');
        return;
    }

    try {
        // Achado (11/09) — leitura errava às vezes mesmo com código nítido.
        // Sem hints, o ZXing tenta TODOS os formatos (incluindo 2D como QR/
        // Data Matrix), gastando tempo/chance em formatos que produto de
        // loja nunca usa. Restringindo aos formatos 1D reais de código de
        // barras de mercadoria + TRY_HARDER (mais lento por frame, mas o
        // acerto importa mais que velocidade aqui — é 1 leitura, não vídeo
        // contínuo), a taxa de acerto melhora bastante.
        const hints = new Map();
        hints.set(ZXing.DecodeHintType.POSSIBLE_FORMATS, [
            ZXing.BarcodeFormat.EAN_13,
            ZXing.BarcodeFormat.EAN_8,
            ZXing.BarcodeFormat.UPC_A,
            ZXing.BarcodeFormat.UPC_E,
            ZXing.BarcodeFormat.CODE_128,
            ZXing.BarcodeFormat.CODE_39,
            ZXing.BarcodeFormat.ITF
        ]);

        _leitor = new ZXing.BrowserMultiFormatReader(hints);

        // Pede resolução mais alta quando o aparelho permitir — mais
        // detalhe pro decodificador enxergar as barras finas de perto.
        await _leitor.decodeFromConstraints(
            {
                video: {
                    facingMode: { ideal: 'environment' },
                    width:  { ideal: 1280 },
                    height: { ideal: 720 }
                }
            },
            video,
            (resultado, erro) => {
                if (resultado) {
                    dotNetHelper.invokeMethodAsync('OnCodigoLido', resultado.getText());
                }
                // erro aqui dispara a cada frame sem código encontrado ainda —
                // não é falha real, é o funcionamento normal enquanto procura.
            }
        );
    } catch (ex) {
        dotNetHelper.invokeMethodAsync('OnErroScanner', ex.message || 'Não foi possível acessar a câmera.');
    }
};

window.pararScannerCodigoBarras = function () {
    if (_leitor) {
        _leitor.reset(); // também libera a câmera que o próprio ZXing abriu
        _leitor = null;
    }
};
