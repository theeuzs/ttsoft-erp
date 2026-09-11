// ── barcode-scanner.js ──────────────────────────────────────────────────
// Leitura de código de barras pela câmera, usando ZXing (wwwroot/lib/zxing.min.js).
// Precisa ser carregado DEPOIS do zxing.min.js.

let _leitor = null;
let _streamAtivo = null;

window.iniciarScannerCodigoBarras = async function (videoElementId, dotNetHelper) {
    const video = document.getElementById(videoElementId);
    if (!video) {
        dotNetHelper.invokeMethodAsync('OnErroScanner', 'Elemento de vídeo não encontrado.');
        return;
    }

    try {
        // Pede a câmera traseira quando disponível (facingMode: environment) —
        // é a câmera certa pra ler código de barras de produto, não a frontal.
        _streamAtivo = await navigator.mediaDevices.getUserMedia({
            video: { facingMode: { ideal: 'environment' } }
        });
        video.srcObject = _streamAtivo;
        await video.play();

        _leitor = new ZXing.BrowserMultiFormatReader();
        _leitor.decodeFromVideoElement(video, (resultado, erro) => {
            if (resultado) {
                dotNetHelper.invokeMethodAsync('OnCodigoLido', resultado.getText());
            }
            // erro aqui é disparado a cada frame sem código encontrado ainda —
            // não é falha real, é o funcionamento normal enquanto procura.
        });
    } catch (ex) {
        // Câmera negada, indisponível, ou navegador sem suporte.
        dotNetHelper.invokeMethodAsync('OnErroScanner', ex.message || 'Não foi possível acessar a câmera.');
    }
};

window.pararScannerCodigoBarras = function () {
    if (_leitor) {
        _leitor.reset();
        _leitor = null;
    }
    if (_streamAtivo) {
        _streamAtivo.getTracks().forEach(t => t.stop());
        _streamAtivo = null;
    }
};
