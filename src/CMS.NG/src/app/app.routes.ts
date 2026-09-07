import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: 'admin/app-users',
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
      }
    ]
  }
];
