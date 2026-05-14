(function () {
	'use strict';

	if (typeof flatpickr === 'undefined') return;

	// Common initializer
	const initFlatpickr = (selector, options) => {
		const el = document.querySelector(selector);
		if (!el || el._flatpickr) return;

		flatpickr(el, options);
	};
	
	document.addEventListener('DOMContentLoaded', function () {

		initFlatpickr('#flatpickr_basic', {
			dateFormat: 'Y-m-d H:i',
			disableMobile: true
		});

		initFlatpickr('#flatpickr_datetime', {
			enableTime: true,
			dateFormat: 'Y-m-d H:i',
			disableMobile: true
		});

		initFlatpickr('#flatpickr_time', {
			noCalendar: true,
			enableTime: true,
			disableMobile: true
		});

		initFlatpickr('#flatpickr_range', {
			mode: 'range'
		});

		initFlatpickr('#flatpickr_multiple_dates', {
			mode: 'multiple'
		});

		initFlatpickr('#flatpickr_localization', {
			enableTime: true,
			dateFormat: 'Y-m-d H:i',
			locale: 'fr',
			disableMobile: true
		});

		initFlatpickr('#flatpickr_inline', {
			enableTime: true,
			dateFormat: 'Y-m-d H:i',
			inline: true
		});

		initFlatpickr('#flatpickr_weeknumbers', {
			weekNumbers: true,
			enableTime: true,
			dateFormat: 'Y-m-d H:i'
		});

	});
	
})();