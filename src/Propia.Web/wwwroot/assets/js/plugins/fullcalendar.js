if (jQuery('#eventStartDate').length > 0) {
    $("#eventStartDate").flatpickr({
        enableTime: true,
        dateFormat: "Y-m-d H:i",
    });
}
if (jQuery('#eventEndDate').length > 0) {
    $("#eventEndDate").flatpickr({
        enableTime: true,
        dateFormat: "Y-m-d H:i",
    });
}

document.addEventListener('DOMContentLoaded', function () {
	
	const Draggable = FullCalendar.Draggable;

    // Draggable external events
    new Draggable(document.getElementById('external-events'), {
        itemSelector: '.fc-event',
        eventData: function (eventEl) {
            return {
                title: eventEl.innerText.trim(),
                backgroundColor: eventEl.getAttribute('data-color'),
                borderColor: eventEl.getAttribute('data-color'),
                textColor: '#fff',
                extendedProps: {
                    label: eventEl.getAttribute('data-label') || '',
                    location: eventEl.getAttribute('data-location') || '',
                    description: eventEl.getAttribute('data-description') || ''
                }
            };
        }
    });

    const calendarEl = document.getElementById('calendar');
    const calendar = new FullCalendar.Calendar(calendarEl, {
        initialView: 'dayGridMonth',
        editable: true,
        droppable: true,
        headerToolbar: {
            left: 'prev,next today',
            center: 'title',
            right: 'dayGridMonth,timeGridWeek,timeGridDay'
        },

        // Default end date +1 day for dragged events
        eventReceive: function (info) {
            if (!info.event.end) {
                const end = new Date(info.event.start);
                end.setDate(end.getDate() + 1);
                info.event.setEnd(end);
            }
        },

        // Event click shows details modal
        eventClick: function (info) {
            info.jsEvent.preventDefault();
            document.getElementById('eventTitle').textContent = info.event.title;
            document.getElementById('eventStart').textContent = info.event.start ? info.event.start.toLocaleString() : 'N/A';
            document.getElementById('eventEnd').textContent = info.event.end ? info.event.end.toLocaleString() : 'N/A';
            document.getElementById('eventLocation').textContent = info.event.extendedProps.location || 'N/A';
            document.getElementById('eventDescription').textContent = info.event.extendedProps.description || 'N/A';

            new bootstrap.Modal(document.getElementById('eventDetailsModal')).show();
        }
    });

    // Predefined events
    const initialEvents = [
        {
            title: "Meeting with Team",
            start: "2025-11-05 10:00",
            end: "2025-11-05 12:00",
            extendedProps: {
				location: "Conference Room",
				description: "Discuss project milestones"
			},
            backgroundColor: "var(--bs-primary)",
			borderColor: "var(--bs-body-bg)",
			textColor: "#fff"
        },
        {
            title: "Client Call",
            start: "2025-11-10 14:00",
            end: "2025-11-10 15:00",
            extendedProps: {
				location: "Zoom",
				description: "Monthly client sync"
			},
            backgroundColor: "var(--bs-success)",
			borderColor: "var(--bs-body-bg)",
			textColor: "#fff"
        },
        {
            title: "Webinar",
            start: "2025-11-15 16:00",
            end: "2025-11-15 18:00",
            extendedProps: {
				location: "Online",
				description: "Learning session on FullCalendar"
			},
            backgroundColor: "var(--bs-warning)",
			borderColor: "var(--bs-body-bg)",
			textColor: "#fff"
        },
        {
            title: "Team Lunch",
            start: "2025-11-20 23:59",
            extendedProps: {
				location: "",
				description: "Final submission for client project"
			},
            backgroundColor: "var(--bs-danger)",
			borderColor: "var(--bs-body-bg)",
			textColor: "#fff"
        },
        {
            title: "Project Deadline",
            start: "2025-11-25 23:59",
            extendedProps: {
				location: "",
				description: "Final submission for client project"
			},
            backgroundColor: "var(--bs-info)",
			borderColor: "var(--bs-body-bg)",
			textColor: "#fff"
        },
        {
            title: "Performance Review",
            start: "2025-11-28 11:00",
            end: "2025-11-28 12:00",
            extendedProps: {
				location: "HR Office",
				description: "Quarterly performance review"
			},
            backgroundColor: "var(--bs-secondary)",
			borderColor: "var(--bs-body-bg)",
			textColor: "#fff"
        }
    ];

    initialEvents.forEach(event => calendar.addEvent(event));

    // Render calendar
    calendar.render();

    // Add Event form submission
    document.getElementById('eventForm').addEventListener('submit', function (e) {
        e.preventDefault();

        const title = document.getElementById('title').value;
        const label = document.getElementById('label').value;
        const start = document.getElementById('eventStartDate').value;
        const end = document.getElementById('eventEndDate').value;
        const url = document.getElementById('url').value;
        const location = document.getElementById('location').value;
        const description = document.getElementById('description').value;

        if (title && start) {
            calendar.addEvent({
                title: title,
                start: start,
                end: end || null,
                url: url || null,
                extendedProps: { label: label || '', location, description },
                backgroundColor: `var(--bs-${label})`,
                borderColor: '#fff',
                textColor: '#fff'
            });

            // Reset form and close modal
            this.reset();
            const modal = bootstrap.Modal.getInstance(document.getElementById('modalAddEvent'));
            if(modal) modal.hide();
        } else {
            alert('Please enter required fields.');
        }
    });
});
