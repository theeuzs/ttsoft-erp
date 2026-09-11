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
        _leitor = new ZXing.BrowserMultiFormatReader();

        // Achado (11/09) — câmera abria e mostrava imagem, mas nunca lia
        // nada. Causa: getUserMedia manual + decodeFromVideoElement
        // separado tem um problema de sincronismo (o leitor começa a
        // procurar antes do vídeo ter frame real pra ler, e nunca
        // resincroniza). decodeFromConstraints cuida dos dois passos junto,
        // de forma coordenada — é o jeito robusto documentado da biblioteca.
        await _leitor.decodeFromConstraints(
            { video: { facingMode: { ideal: 'environment' } } },
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