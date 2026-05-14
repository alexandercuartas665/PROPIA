document.addEventListener('DOMContentLoaded', () => {

    // Sortable with handle
    const sortableHandle = document.getElementById('sortableHandle');
    if (sortableHandle) {
        new Sortable(sortableHandle, {
            handle: '.sortable-handle',
            animation: 150
        });
    }

    // Basic sortable
    const sortableBasic = document.getElementById('sortableBasic');
    if (sortableBasic) {
        new Sortable(sortableBasic, {
            animation: 150
        });
    }

    // Cloning sortables
    const sortableCloning1 = document.getElementById('sortableCloning1');
    const sortableCloning2 = document.getElementById('sortableCloning2');

    if (sortableCloning1) {
        new Sortable(sortableCloning1, {
            group: {
                name: 'shared',
                pull: 'clone'
            },
            animation: 150
        });
    }

    if (sortableCloning2) {
        new Sortable(sortableCloning2, {
            group: {
                name: 'shared',
                pull: 'clone'
            },
            animation: 150
        });
    }

    // Grid sortable
    const sortableGrid = document.getElementById('sortableGrid');
    if (sortableGrid) {
        new Sortable(sortableGrid, {
            swapThreshold: 1,
            animation: 150
        });
    }

});
