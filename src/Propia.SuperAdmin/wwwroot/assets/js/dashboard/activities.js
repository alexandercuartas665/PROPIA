if ($('#dt_Activities').length) {
	const dt_Activities = $('#dt_Activities').DataTable({
		searching: true,
		pageLength: 6,
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
			var dtSearch = $('#dt_Activities_wrapper .dt-search').detach();
			$('#dt_Activities_Search').append(dtSearch);
			$('#dt_Activities_Search .dt-search').prepend('<i class="fi fi-rr-search"></i>');
			$('#dt_Activities_Search .dt-search label').remove();
			$('#dt_Activities_wrapper > .row.mt-2.justify-content-between').first().remove();
		},
		columnDefs: [{
			targets: [0],
			orderable: false,
		}]
	});
}


var callsChartConfig = {
	series: [
		{ name: 'Calls', data: [40, 55, 38, 62, 70, 68, 80] },
		{ name: 'Tasks', data: [20, 27, 19, 31, 45, 34, 40] },
		{ name: 'Leads', data: [10, 13, 9, 15, 28, 17, 20] }
	],
	chart: {
		type: 'line',
		height: 300,
		zoom: { enabled: false },
		toolbar: { show: false },
	},
	dataLabels: { enabled: false },
	stroke: {
		width: [3],
		curve: 'smooth',
		dashArray: [0, 8, 5]
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
	colors: [
		"var(--bs-primary)",
		"var(--bs-secondary)",
		"var(--bs-warning)"
	],
	yaxis: {
		labels: {
			style: {
				colors: 'var(--bs-body-color)',
				fontSize: '13px',
				fontWeight: '500',
				fontFamily: 'var(--bs-body-font-family)'
			}
		}
	},
	xaxis: {
		categories: ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'],
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
	legend: {
		show: false
	},
	grid: {
		borderColor: 'var(--bs-border-color)',
		strokeDashArray: 5,
		xaxis: { lines: { show: false } },
		yaxis: { lines: { show: true } }
	}
};
const callsChart = document.querySelector("#callsChart");
if (callsChart) {
	new ApexCharts(callsChart, callsChartConfig).render();
}



var tasksChartConfig = {
	chart: {
		type: 'bar',
		height: 300,
		toolbar: {
			show: false
		}
	},
	series: [{
		name: 'Completed Tasks',
		data: [12, 18, 14, 22, 25, 20, 30]
	}],
	plotOptions: {
		bar: {
			columnWidth: '45%',
			borderRadius: 2
		}
	},
	yaxis: {
		labels: {
			style: {
				colors: 'var(--bs-body-color)',
				fontSize: '13px',
				fontWeight: '500',
				fontFamily: 'var(--bs-body-font-family)'
			}
		}
	},
	xaxis: {
		categories: ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'],
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
	colors: [
		"var(--bs-primary)"
	],
	dataLabels: {
		enabled: false
	},
	grid: {
		borderColor: 'var(--bs-border-color)',
		strokeDashArray: 5,
		xaxis: { lines: { show: false } },
		yaxis: { lines: { show: true } }
	}
};
const tasksChart = document.querySelector("#tasksChart");
if (tasksChart) {
	new ApexCharts(tasksChart, tasksChartConfig).render();
}



var leadsChartConfig = {
	chart: {
		type: 'donut',
		height: 260
	},
	series: [35, 20, 45],
	labels: ['Closed', 'In Progress', 'New'],

	plotOptions: {
		pie: {
			donut: {
				size: '65%',
				labels: {
					show: true,
					value: {
						fontSize: '22px',
						fontWeight: 600,
						fontFamily: 'var(--bs-body-font-family)'
					},
					total: {
						show: true,
						label: 'Total Leads',
						formatter: function (w) {
							return 35 + 20 + 45;
						}
					},
					name: {
						fontFamily: 'var(--bs-body-font-family)',
                        color: 'var(-bs-body-color)'
                    }
				}
			}
		}
	},

	colors: [
		"var(--bs-primary)",
		"var(--bs-info)",
		"var(--bs-success)"
	],

	dataLabels: {
		enabled: false
	},
	stroke: {
		width: 0
	},
	legend: {
		show: false
	},
	tooltip: { enabled: false }
};
const leadsChart = document.querySelector("#leadsChart");
if (leadsChart) {
	new ApexCharts(leadsChart, leadsChartConfig).render();
}