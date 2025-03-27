// Crea este archivo en wwwroot/js/file-upload.js

// Función para inicializar los controladores de drag & drop en los contenedores de carga de archivos
function initFileUploaders() {
    // Buscar todos los contenedores de carga
    document.querySelectorAll('.border-dashed').forEach(container => {
        // Encontrar el input file asociado
        const input = container.querySelector('input[type="file"]');
        if (!input) return;

        // Asignar evento clic al contenedor para abrir el selector de archivos
        container.addEventListener('click', (e) => {
            // Evitar abrir el selector si el clic fue en un botón
            if (e.target.closest('button')) return;
            input.click();
        });

        // Eventos de drag & drop
        container.addEventListener('dragover', (e) => {
            e.preventDefault();
            container.classList.add('border-blue-500', 'bg-blue-100');
        });

        container.addEventListener('dragleave', (e) => {
            e.preventDefault();
            container.classList.remove('border-blue-500', 'bg-blue-100');
        });

        container.addEventListener('drop', (e) => {
            e.preventDefault();
            container.classList.remove('border-blue-500', 'bg-blue-100');

            // Si el input tiene un controlador Blazor asociado, no podemos
            // simular una carga directamente, pero podemos transferir los archivos
            if (e.dataTransfer.files.length > 0) {
                // Crea un nuevo objeto de evento con los archivos arrastrados
                const newEvent = new Event('change', { bubbles: true });

                // Agrega los archivos al input
                Object.defineProperty(newEvent, 'target', {
                    writable: false,
                    value: { files: e.dataTransfer.files }
                });

                // Dispara el evento para que Blazor lo maneje
                input.dispatchEvent(newEvent);
            }
        });
    });
}

// Función para registrar un MutationObserver que detecte nuevos contenedores
function observeForFileUploaders() {
    // Crear un observador para detectar cambios en el DOM
    const observer = new MutationObserver((mutations) => {
        for (const mutation of mutations) {
            if (mutation.addedNodes.length) {
                // Comprobar si se añadieron nuevos contenedores de carga
                const hasNewUploaders = Array.from(mutation.addedNodes)
                    .some(node => {
                        if (node.querySelectorAll) {
                            return node.querySelectorAll('.border-dashed').length > 0;
                        }
                        return false;
                    });

                if (hasNewUploaders) {
                    // Si hay nuevos contenedores, inicializarlos
                    initFileUploaders();
                }
            }
        }
    });

    // Comenzar a observar cambios en el cuerpo del documento
    observer.observe(document.body, { childList: true, subtree: true });

    // También inicializar los contenedores existentes
    initFileUploaders();
}

// Inicializar cuando el DOM esté listo
document.addEventListener('DOMContentLoaded', () => {
    initFileUploaders();
    observeForFileUploaders();
});

// Función para inicializar después de una actualización de Blazor
window.initFileUploaders = initFileUploaders;

// Optimización de carga de archivos para Blazor Server
window.uploadFileInChunks = async function (inputId, maxChunkSize) {
    const input = document.getElementById(inputId);
    if (!input || !input.files || input.files.length === 0) return null;

    const file = input.files[0];
    const totalSize = file.size;
    const totalChunks = Math.ceil(totalSize / maxChunkSize);
    const fileInfo = {
        name: file.name,
        size: file.size,
        type: file.contentType,
        lastModified: file.lastModified,
        totalChunks: totalChunks
    };

    return fileInfo;
};

window.readFileChunkAsync = async function (inputId, index, chunkSize) {
    return new Promise((resolve, reject) => {
        const input = document.getElementById(inputId);
        if (!input || !input.files || input.files.length === 0) {
            reject("No file selected");
            return;
        }

        const file = input.files[0];
        const fileReader = new FileReader();
        const start = index * chunkSize;
        let end = start + chunkSize;
        if (end > file.size) end = file.size;

        fileReader.onload = e => resolve(e.target.result);
        fileReader.onerror = () => reject("Error reading file");

        const slice = file.slice(start, end);
        fileReader.readAsArrayBuffer(slice);
    });
};