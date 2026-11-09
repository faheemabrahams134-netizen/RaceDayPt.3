// RaceDay: small progressive enhancements. Every page still works without this file.
(function () {
  // Confirm before destructive actions: <button data-confirm="Delete this?">
  document.addEventListener('click', function (e) {
    var el = e.target.closest('[data-confirm]');
    if (el && !window.confirm(el.getAttribute('data-confirm'))) e.preventDefault();
  });

  // Show/hide fields depending on a <select data-toggle-target>
  function bindToggle(select) {
    var apply = function () {
      document.querySelectorAll('[data-show-when]').forEach(function (el) {
        var wanted = el.getAttribute('data-show-when').split(',');
        var on = wanted.indexOf(select.value) !== -1;
        el.classList.toggle('d-none', !on);
      });
    };
    select.addEventListener('change', apply);
    apply();
  }
  document.querySelectorAll('select[data-toggle-fields], input[type=radio][data-toggle-fields]').forEach(function (el) {
    if (el.tagName === 'SELECT') bindToggle(el);
  });

  // Registration: radio buttons choose which fields to show
  var roleRadios = document.querySelectorAll('input[name="Role"][data-role-toggle]');
  if (roleRadios.length) {
    var applyRole = function () {
      var checked = document.querySelector('input[name="Role"][data-role-toggle]:checked');
      var role = checked ? checked.value : '';
      document.querySelectorAll('[data-role-only]').forEach(function (el) {
        el.classList.toggle('d-none', el.getAttribute('data-role-only') !== role);
      });
    };
    roleRadios.forEach(function (r) { r.addEventListener('change', applyRole); });
    applyRole();
  }

  // Image pickers: preview + size check
  document.querySelectorAll('input[type=file][data-preview]').forEach(function (input) {
    input.addEventListener('change', function () {
      var file = input.files && input.files[0];
      var target = document.getElementById(input.getAttribute('data-preview'));
      var warn = document.getElementById(input.getAttribute('data-preview') + '-warn');
      if (warn) warn.textContent = '';
      if (!file) return;
      if (file.size > 5 * 1024 * 1024) {
        if (warn) warn.textContent = 'That image is larger than 5 MB. Please choose a smaller one.';
        input.value = '';
        return;
      }
      if (target) {
        target.src = URL.createObjectURL(file);
        target.classList.remove('d-none');
      }
    });
  });
})();
