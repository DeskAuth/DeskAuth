# Security policy

DeskAuth stores people's two-factor secret keys, so security problems are taken seriously.

## Reporting a vulnerability

**Please don't report security problems in a public GitHub issue.** Email **security@deskauth.com** instead, with:

* what the problem is and what an attacker could do with it
* the steps to reproduce it, and the DeskAuth and Windows versions you used
* any proof of concept, if you have one

Never send real secret keys, passwords or `deskauth.xml` files; make test ones instead.

You should get a reply within a few days. We'll let you know whether we can reproduce the problem, work on a fix, and publish a new release. We're happy to credit you in the release notes, unless you'd rather not be named. Please give us a reasonable time to release a fix before disclosing the problem publicly.

## Supported versions

Only the [latest release](https://github.com/DeskAuth/DeskAuth/releases/latest) receives security fixes. DeskAuth tells you when a new version is available.

## Scope

In scope: the DeskAuth app, the release files on GitHub, the build and release workflows in this repository, and the deskauth.com website.

DeskAuth cannot protect secret keys from malware already running as you on your computer, which can read your files and watch you type your password; see [Is it secure?](https://deskauth.com/help.html#secure). Problems that need such access are still worth reporting if DeskAuth makes an attack easier than it needs to be.
