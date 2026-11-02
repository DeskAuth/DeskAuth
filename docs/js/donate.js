// "Copy address" buttons on the donate page
document.querySelectorAll('.copy-address').forEach(function (button) {
  var label = button.textContent;
  button.addEventListener('click', function () {
    var address = button.parentElement.querySelector('.address').textContent;
    navigator.clipboard.writeText(address).then(function () {
      button.textContent = 'Copied';
    }, function () {
      button.textContent = 'Copy failed: select the address';
    });
    setTimeout(function () { button.textContent = label; }, 2500);
  });
});
