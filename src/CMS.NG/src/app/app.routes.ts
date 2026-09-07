import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: 'admin/app-roles',
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
  }
];
