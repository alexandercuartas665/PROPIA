if ($('#dt_UserList').length) {
	const dt_UserList = $('#dt_UserList').DataTable({
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
			var dtSearch = $('#dt_UserList_wrapper .dt-search').detach();
			$('#dt_UserList_Search').append(dtSearch);
			$('#dt_UserList_Search .dt-search').prepend('<i class="fi fi-rr-search"></i>');
			$('#dt_UserList_Search .dt-search label').remove();
			$('#dt_UserList_wrapper > .row.mt-2.justify-content-between').first().remove();
		},
		columnDefs: [{
			targets: [0],
			orderable: false,
		}]
	});
}