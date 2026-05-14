document.addEventListener('DOMContentLoaded', () => {

	const toastTrigger = document.getElementById('liveToastBtn');
	const toastLiveExample = document.getElementById('liveToast');

	if (toastTrigger && toastLiveExample && window.bootstrap) {
		const toastBootstrap = bootstrap.Toast.getOrCreateInstance(toastLiveExample);

		toastTrigger.addEventListener('click', () => {
			toastBootstrap.show();
		});
	}

});
