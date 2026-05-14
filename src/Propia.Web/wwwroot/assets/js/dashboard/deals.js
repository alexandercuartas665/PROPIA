if ($('#dt_NewCustomers').length) {
	const dt_NewCustomers = $('#dt_NewCustomers').DataTable({
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
			var dtSearch = $('#dt_NewCustomers_wrapper .dt-search').detach();
			$('#dt_NewCustomers_Search').append(dtSearch);
			$('#dt_NewCustomers_Search .dt-search').prepend('<i class="fi fi-rr-search"></i>');
			$('#dt_NewCustomers_Search .dt-search label').remove();
			$('#dt_NewCustomers_wrapper > .row.mt-2.justify-content-between').first().remove();
		},
		columnDefs: [{
			targets: [0],
			orderable: false,
		}]
	});
}


const dealsValueTrendChartConfig = {
	series: [
		{
			name: 'Income',
			data: [3500, 5000, 4200, 5500, 5000, 6200, 4800, 6500, 5800, 7200, 6600, 7500]
		},
		{
			name: "Expenses",
			data: [2500, 3100, 2900, 3700, 3300, 4100, 3600, 3900, 4200, 4000, 4600, 4300]
		}
	],
	chart: {
		height: 320,
		type: 'area',
		zoom: { enabled: false },
		toolbar: { show: false },
	},
	colors: [
		"var(--bs-primary)",
		"var(--bs-secondary)"
	],
	fill: {
		type: ["gradient", "gradient"],
		gradient: {
			shade: 'light',
			type: "vertical",
			shadeIntensity: 0.1,
			gradientToColors: ["var(--bs-primary)"],
			inverseColors: false,
			opacityFrom: 0.08,
			opacityTo: 0.01,
			stops: [20, 100]
		},
		gradient: {
			shade: 'light',
			type: "vertical",
			shadeIntensity: 0.1,
			gradientToColors: ["var(--bs-secondary)"],
			inverseColors: false,
			opacityFrom: 0.06,
			opacityTo: 0.01,
			stops: [20, 100]
		}
	},
	dataLabels: { enabled: false },
	stroke: {
		width: [2, 2],
		curve: 'smooth',
		dashArray: [0, 5]
	},
	markers: {
		size: 0,
		colors: ['#FFFFFF'],
		strokeColors: 'var(--bs-primary)',
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
				return "$" + (value / 100) + "K";
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
	tooltip: {
		y: {
			formatter: function (val) {
				return "$ " + val + "K";
			}
		}
	},
	grid: {
		borderColor: 'var(--bs-border-color)',
		strokeDashArray: 5,
		xaxis: { lines: { show: false } },
		yaxis: { lines: { show: true } }
	},
	legend: {
		show: false
	}
};
const chartDeals = document.querySelector("#chartDeals");
if (chartDeals) {
	const chartTabsInit = new ApexCharts(chartDeals, dealsValueTrendChartConfig);
	chartTabsInit.render();

	document.querySelector("#todayDealsTab").addEventListener("click", () => {
		chartTabsInit.updateOptions({
			xaxis: {
				categories: ['2 AM', '4 AM', '6 AM', '8 AM', '10 AM', '12 PM', '2 PM', '4 PM', '6 PM', '8 PM']
			},
			series: [
				{
					name: 'Income',
					data: [3500, 5000, 4200, 5500, 5000, 6200, 4800, 6500, 5800, 7200]
				},
				{
					name: "Expenses",
					data: [2500, 3100, 2900, 3700, 3300, 4100, 3600, 3900, 4200, 4000]
				}
			],
		});
	});

	document.querySelector("#weekDealsTab").addEventListener("click", () => {
		chartTabsInit.updateOptions({
			xaxis: {
				categories: ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun']
			},
			series: [
				{
					name: 'Income',
					data: [3500, 5000, 4200, 5500, 5000, 6200, 4800]
				},
				{
					name: "Expenses",
					data: [2500, 3100, 2900, 3700, 3300, 4100, 3600]
				}
			],
		});
	});

	document.querySelector("#monthDealsTab").addEventListener("click", () => {
		chartTabsInit.updateOptions({
			xaxis: {
				categories: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'],
			},
			series: [
				{
					name: 'Income',
					data: [3500, 5000, 4200, 5500, 5000, 6200, 4800, 6500, 5800, 7200, 6600, 7500]
				},
				{
					name: "Expenses",
					data: [2500, 3100, 2900, 3700, 3300, 4100, 3600, 3900, 4200, 4000, 4600, 4300]
				}
			],
		});
	});
}


var chartDealPipelineConfig = {
	series: [
		{
			name: 'Deals',
			data: [850, 550, 1210, 950, 750, 1520, 1310]
		},
		{
			name: 'Value ($)',
			data: [960000, 810000, 720000, 610000, 490000, 1830000, 400000]
		}
	],
	chart: {
		type: 'bar',
		height: 340,
		stacked: false,
		toolbar: {
			show: false
		}
	},
	xaxis: {
		categories: ['New Lead', 'Contacted', 'Qualified', 'Proposal Sent', 'Negotiation', 'Closed Won', 'Closed Lost'],
		axisBorder: { show: false },
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
	colors: ["var(--bs-primary)", "rgba(var(--bs-primary-rgb),0.1)"],
	dataLabels: {
		enabled: false
	},
	plotOptions: {
		bar: {
			horizontal: false,
			columnWidth: '55%',
			borderRadius: 3,
			distributed: false,
		}
	},
	yaxis: [
		{
			labels: {
				style: { colors: 'var(--bs-body-color)', fontSize: '13px', fontWeight: '500' }
			},
		},
		{
			opposite: true,
			labels: {
				formatter: function (value) {
					return "$" + (value / 10000) + "K";
				},
				style: {
					colors: 'var(--bs-body-color)',
					fontSize: '13px',
					fontWeight: '500',
					fontFamily: 'var(--bs-body-font-family)'
				}
			}
		}
	],
	tooltip: {
		y: {
			formatter: function (val, opts) {
				if (opts.seriesIndex === 1) {
					return '$' + val.toLocaleString();
				}
				return val;
			}
		}
	},
	grid: {
		borderColor: 'var(--bs-border-color)',
		strokeDashArray: 5,
		xaxis: { lines: { show: true } },
		yaxis: { lines: { show: true } }
	},
	legend: {
		show: false,
	}
};
const chartDealPipeline = document.querySelector("#chartDealPipeline");
if (chartDealPipeline) {
	new ApexCharts(chartDealPipeline, chartDealPipelineConfig).render();
}