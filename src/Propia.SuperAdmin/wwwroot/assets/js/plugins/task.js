document.addEventListener('DOMContentLoaded', () => {

    if (!window.Sortable) return;

    const taskWrappers = [
        document.getElementById('taskWrapper1'),
        document.getElementById('taskWrapper2'),
        document.getElementById('taskWrapper3'),
        document.getElementById('taskWrapper4')
    ];

    taskWrappers.forEach(wrapper => {
        if (wrapper) {
            new Sortable(wrapper, {
                group: 'shared',
                animation: 150
            });
        }
    });

});
