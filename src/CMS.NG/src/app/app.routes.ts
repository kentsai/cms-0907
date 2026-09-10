import { Routes } from '@angular/router';
import { adminGuard } from '@core/guards/admin.guard';
import { authGuard } from '@core/guards/auth.guard';

/** Every signed-in page. Wrapped by `routes` below, which guards the whole group. */
const featureRoutes: Routes = [
  {
    // Weekly board with inline edit — a single page, no detail / form routes.
    path: 'home/featured-promo-items',
    loadComponent: () =>
      import('@features/featured-promo-items/featured-promo-list/featured-promo-list.component')
        .then(m => m.FeaturedPromoListComponent)
  },
  {
    // Administrators only, mirroring the API's Admin policy on AppUsersController and the app-roles lookup.
    path: 'admin/app-users',
    canActivate: [adminGuard],
    children: [
      {
        path: '',
        loadComponent: () =>
          import('@features/app-users/app-user-list/app-user-list.component')
            .then(m => m.AppUserListComponent)
      },
      // `new` must precede `:id` so it is not captured as a UserId.
      {
        path: 'new',
        loadComponent: () =>
          import('@features/app-users/app-user-form/app-user-form.component')
            .then(m => m.AppUserFormComponent)
      },
      {
        path: ':id',
        loadComponent: () =>
          import('@features/app-users/app-user-detail/app-user-detail.component')
            .then(m => m.AppUserDetailComponent)
      },
      {
        path: ':id/edit',
        loadComponent: () =>
          import('@features/app-users/app-user-form/app-user-form.component')
            .then(m => m.AppUserFormComponent)
      }
    ]
  },
  {
    // Administrators only, mirroring the API's Admin policy on AppRolesController and the app-users lookup.
    path: 'admin/app-roles',
    canActivate: [adminGuard],
    children: [
      {
        path: '',
        loadComponent: () =>
          import('@features/app-roles/app-role-list/app-role-list.component')
            .then(m => m.AppRoleListComponent)
      },
      // `new` must precede `:id` so it is not captured as a RoleId.
      {
        path: 'new',
        loadComponent: () =>
          import('@features/app-roles/app-role-form/app-role-form.component')
            .then(m => m.AppRoleFormComponent)
      },
      {
        path: ':id',
        loadComponent: () =>
          import('@features/app-roles/app-role-detail/app-role-detail.component')
            .then(m => m.AppRoleDetailComponent)
      },
      {
        path: ':id/edit',
        loadComponent: () =>
          import('@features/app-roles/app-role-form/app-role-form.component')
            .then(m => m.AppRoleFormComponent)
      }
    ]
  },
  {
    path: 'admin/publish-statuses',
    children: [
      {
        path: '',
        loadComponent: () =>
          import('@features/publish-statuses/publish-status-list/publish-status-list.component')
            .then(m => m.PublishStatusListComponent)
      },
      // `new` must precede `:id` so it is not captured as an id.
      {
        path: 'new',
        loadComponent: () =>
          import('@features/publish-statuses/publish-status-form/publish-status-form.component')
            .then(m => m.PublishStatusFormComponent)
      },
      {
        path: ':id',
        loadComponent: () =>
          import('@features/publish-statuses/publish-status-detail/publish-status-detail.component')
            .then(m => m.PublishStatusDetailComponent)
      },
      {
        path: ':id/edit',
        loadComponent: () =>
          import('@features/publish-statuses/publish-status-form/publish-status-form.component')
            .then(m => m.PublishStatusFormComponent)
      }
    ]
  },
  {
    path: 'course/partners',
    children: [
      {
        path: '',
        loadComponent: () =>
          import('@features/partners/partner-list/partner-list.component')
            .then(m => m.PartnerListComponent)
      },
      // `new` must precede `:id` so it is not captured as an id.
      {
        path: 'new',
        loadComponent: () =>
          import('@features/partners/partner-form/partner-form.component')
            .then(m => m.PartnerFormComponent)
      },
      {
        path: ':id',
        loadComponent: () =>
          import('@features/partners/partner-detail/partner-detail.component')
            .then(m => m.PartnerDetailComponent)
      },
      {
        path: ':id/edit',
        loadComponent: () =>
          import('@features/partners/partner-form/partner-form.component')
            .then(m => m.PartnerFormComponent)
      }
    ]
  },
  {
    path: 'course/course-groups',
    children: [
      {
        path: '',
        loadComponent: () =>
          import('@features/course-groups/course-group-list/course-group-list.component')
            .then(m => m.CourseGroupListComponent)
      },
      // `new` must precede `:id` so it is not captured as an id.
      {
        path: 'new',
        loadComponent: () =>
          import('@features/course-groups/course-group-form/course-group-form.component')
            .then(m => m.CourseGroupFormComponent)
      },
      {
        path: ':id',
        loadComponent: () =>
          import('@features/course-groups/course-group-detail/course-group-detail.component')
            .then(m => m.CourseGroupDetailComponent)
      },
      {
        path: ':id/edit',
        loadComponent: () =>
          import('@features/course-groups/course-group-form/course-group-form.component')
            .then(m => m.CourseGroupFormComponent)
      }
    ]
  },
  {
    path: 'course/courses',
    children: [
      {
        path: '',
        loadComponent: () =>
          import('@features/courses/course-list/course-list.component')
            .then(m => m.CourseListComponent)
      },
      // `new` must precede `:id` so it is not captured as an id.
      {
        path: 'new',
        loadComponent: () =>
          import('@features/courses/course-form/course-form.component')
            .then(m => m.CourseFormComponent)
      },
      {
        path: ':id',
        loadComponent: () =>
          import('@features/courses/course-detail/course-detail.component')
            .then(m => m.CourseDetailComponent)
      },
      {
        path: ':id/edit',
        loadComponent: () =>
          import('@features/courses/course-form/course-form.component')
            .then(m => m.CourseFormComponent)
      },
      {
        // 列印PDF: customer-facing print view opened in a new tab; `chromeless` drops the shell (see app.ts).
        path: ':id/print',
        data: { chromeless: true },
        loadComponent: () =>
          import('@features/courses/course-print/course-print.component')
            .then(m => m.CoursePrintComponent)
      }
    ]
  },
  {
    path: 'course/certifications',
    children: [
      {
        path: '',
        loadComponent: () =>
          import('@features/certifications/certification-list/certification-list.component')
            .then(m => m.CertificationListComponent)
      },
      // `new` must precede `:id` so it is not captured as an id.
      {
        path: 'new',
        loadComponent: () =>
          import('@features/certifications/certification-form/certification-form.component')
            .then(m => m.CertificationFormComponent)
      },
      {
        path: ':id',
        loadComponent: () =>
          import('@features/certifications/certification-detail/certification-detail.component')
            .then(m => m.CertificationDetailComponent)
      },
      {
        path: ':id/edit',
        loadComponent: () =>
          import('@features/certifications/certification-form/certification-form.component')
            .then(m => m.CertificationFormComponent)
      }
    ]
  }
];

export const routes: Routes = [
  {
    // Public: the only page reachable without a token.
    path: 'login',
    loadComponent: () => import('@features/auth/login/login.component').then(m => m.LoginComponent)
  },
  {
    // Everything else requires a token in session storage (see authGuard); otherwise → /login?returnUrl=…
    path: '',
    canActivateChild: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'home/featured-promo-items' },
      {
        // 個人資料 My Profile — the signed-in user's own UserName.
        path: 'profile',
        loadComponent: () => import('@features/auth/profile/profile.component').then(m => m.ProfileComponent)
      },
      {
        // 變更密碼 — the only page a default-password login may use (authGuard sends it here from every other URL).
        path: 'change-password',
        loadComponent: () =>
          import('@features/auth/change-password/change-password.component').then(m => m.ChangePasswordComponent)
      },
      ...featureRoutes
    ]
  }
];
