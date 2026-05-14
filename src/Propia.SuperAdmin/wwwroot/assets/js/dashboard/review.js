if ($('#dt_RecentReviews').length) {
	const dt_RecentReviews = $('#dt_RecentReviews').DataTable({
		searching: true,
		pageLength: 5,
		select: false,
		lengthChange: false,
		info: true,
		paging: true,
		language: {
			search: "",
			searchPlaceholder: 'Search',
			paginate: {
				previous: "<i class='fi fi-rr-angle-left'></i>",
				next: "<i class='fi fi-rr-angle-right'></i>",
				first: "<i class='fi fi-rr-angle-double-left'></i>",
				last: "<i class='fi fi-rr-angle-double-right'></i>"
			},
		},
		initComplete: function () {
			var dtSearch = $('#dt_RecentReviews_wrapper .dt-search').detach();
			$('#dt_RecentReviews_Search').append(dtSearch);
			$('#dt_RecentReviews_Search .dt-search').prepend('<i class="fi fi-rr-search"></i>');
			$('#dt_RecentReviews_Search .dt-search label').remove();
			$('#dt_RecentReviews_wrapper > .row.mt-2.justify-content-between').first().remove();
		},
		columnDefs: [{
			targets: [0],
			orderable: false,
		}]
	});
}

if ($('#dt_TopRated').length) {
	const dt_TopRated = $('#dt_TopRated').DataTable({
		searching: true,
		pageLength: 5,
		select: false,
		lengthChange: false,
		info: true,
		paging: true,
		language: {
			search: "",
			searchPlaceholder: 'Search',
			paginate: {
				previous: "<i class='fi fi-rr-angle-left'></i>",
				next: "<i class='fi fi-rr-angle-right'></i>",
				first: "<i class='fi fi-rr-angle-double-left'></i>",
				last: "<i class='fi fi-rr-angle-double-right'></i>"
			},
		},
		initComplete: function () {
			var dtSearch = $('#dt_TopRated_wrapper .dt-search').detach();
			$('#dt_TopRated_Search').append(dtSearch);
			$('#dt_TopRated_Search .dt-search').prepend('<i class="fi fi-rr-search"></i>');
			$('#dt_TopRated_Search .dt-search label').remove();
			$('#dt_TopRated_wrapper > .row.mt-2.justify-content-between').first().remove();
		},
		columnDefs: [{
			targets: [0],
			orderable: false,
		}]
	});
}

var reviewSourcesChartConfig = {
	series: [
		{
			name: 'Website',
			data: [30]
		},
		{
			name: 'Google',
			data: [25]
		},
		{
			name: 'App Store',
			data: [20]
		},
		{
			name: 'Play Store',
			data: [15]
		},
		{
			name: 'Social Media',
			data: [10]
		}
	],
	chart: {
		type: 'bar',
		height: 95,
		stacked: true,
		stackType: '100%',
		toolbar: {
			show: false
		},
		animations: {
			enabled: true
		},
	},
	plotOptions: {
		bar: {
			horizontal: true,
			barHeight: '100%',
			borderRadius: 0
		}
	},
	dataLabels: {
		enabled: false
	},
	stroke: {
		width: 0,
		colors: ['#ffffff'],
	},
	xaxis: {
		labels: {
			show: false
		},
		axisBorder: {
			show: false
		},
		axisTicks: {
			show: false
		}
	},
	yaxis: {
		labels: {
			show: false
		}
	},
	grid: {
		show: false,
		padding: {
			top: -15,
			bottom: -15,
			left: -15,
			right: 0
		}
	},
	legend: {
		show: false
	},
	fill: {
		opacity: 1,
		colors: [
			'rgba(var(--bs-primary-rgb), 0.1)',
			'rgba(var(--bs-primary-rgb), 0.25)',
			'rgba(var(--bs-primary-rgb), 0.50)',
			'rgba(var(--bs-primary-rgb), 0.75)',
			'rgba(var(--bs-primary-rgb), 1)'
		]
	},
	tooltip: {
		enabled: true,
		y: {
			formatter: function (val, opts) {
				let original = opts.series[opts.seriesIndex][0];
				return original + "%";
			}
		},
		x: {
			show: false
		},
		marker: {
			show: false
		}
	}
};
const reviewSourcesChart = document.querySelector("#reviewSourcesChart");
if (reviewSourcesChart) {
	var chartInit = new ApexCharts(reviewSourcesChart, reviewSourcesChartConfig);
	chartInit.render();
}


var reviewTrendChartConfig = {
	series: [
		{
			name: 'Reviews',
			data: [1500, 4000, 4200, 5500, 4000, 5200, 7800, 6200, 5000, 4200, 7000, 7950]
		}
	],
	chart: {
		height: 270,
		type: 'area',
		zoom: { enabled: false },
		toolbar: { show: false },
	},
	colors: [
		"var(--bs-primary)"
	],
	fill: {
		type: ["gradient"],
		gradient: {
			shade: 'light',
			type: "vertical",
			shadeIntensity: 0.1,
			gradientToColors: ["var(--bs-primary)"],
			inverseColors: false,
			opacityFrom: 0.08,
			opacityTo: 0.01,
			stops: [20, 100]
		}
	},
	dataLabels: { enabled: false },
	stroke: {
		width: [3],
		curve: 'smooth',
		dashArray: [0, 5]
	},
	markers: {
		size: 0,
		colors: ['#FFFFFF'],
		strokeColors: 'var(--bs-info)',
		strokeWidth: 2,
		hover: {
			size: 6
		}
	},
	yaxis: {
		min: 0,
		max: 8000,
		tickAmount: 5,
		labels: {
			formatter: function (value) {
				return "" + (value / 100) + "K";
			},
			style: {
				colors: 'var(--bs-body-color)',
				fontSize: '13px',
				fontWeight: '500',
				fontFamily: 'var(--bs-body-font-family)'
			}
		}
	},
	xaxis: {
		categories: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'],
		axisBorder: { color: 'var(--bs-border-color)' },
		axisTicks: { show: false },
		labels: {
			style: {
				colors: 'var(--bs-body-color)',
				fontSize: '13px',
				fontWeight: '500',
				fontFamily: 'var(--bs-body-font-family)'
			}
		}
	},
	grid: {
		borderColor: 'var(--bs-border-color)',
		strokeDashArray: 5,
		xaxis: { lines: { show: false } },
		yaxis: { lines: { show: true } }
	},
	tooltip: {
		y: {
			formatter: function (val) {
				return "" + val + "K";
			}
		}
	},
	legend: {
		show: true,
		position: 'bottom',
		horizontalAlign: 'center',
		markers: {
			size: 5,
			shape: 'circle',
			radius: 10,
			width: 10,
			height: 10,
		},
		labels: {
			colors: 'var(--bs-heading-color)',
			fontFamily: 'var(--bs-body-font-family)',
			fontSize: '13px',
		}
	}
};
const reviewTrendChart = document.querySelector("#reviewTrendChart");
if (reviewTrendChart) {
	const chartTabsInit = new ApexCharts(reviewTrendChart, reviewTrendChartConfig);
	chartTabsInit.render();

	document.querySelector("#todayReviewTrendTab").addEventListener("click", () => {
		chartTabsInit.updateOptions({
			xaxis: {
				categories: ['2 AM', '4 AM', '6 AM', '8 AM', '10 AM', '12 PM', '2 PM', '4 PM', '6 PM', '8 PM', '10 PM', '12 AM']
			},
			series: [
				{
					name: 'Reviews',
					data: [1500, 4200, 4500, 5500, 3800, 5200, 7800, 6000, 5000, 4200, 7000, 7950]
				}
			],
		});
	});

	document.querySelector("#weekReviewTrendTab").addEventListener("click", () => {
		chartTabsInit.updateOptions({
			xaxis: {
				categories: ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun']
			},
			series: [
				{
					name: 'Reviews',
					data: [1500, 4000, 4200, 6200, 5000, 4200, 7000]
				}
			],
		});
	});

	document.querySelector("#monthReviewTrendTab").addEventListener("click", () => {
		chartTabsInit.updateOptions({
			xaxis: {
				categories: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'],
			},
			series: [
				{
					name: 'Reviews',
					data: [1500, 4000, 4200, 5500, 4000, 5200, 7800, 6200, 5000, 4200, 7000, 7950]
				}
			],
		});
	});
}